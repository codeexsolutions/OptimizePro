using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using OptimizePro.Core.Impressoras;

namespace OptimizePro.Services.Impressoras;

/// <summary>
/// Porte de <c>impressoras/services/discovery.js</c> (§22) — acha impressoras na rede local
/// sem nenhum protocolo direto com a máquina: testa a porta do SMB (445), pergunta o nome
/// NetBIOS, lista os compartilhamentos e reconhece o tipo pelos arquivos que cada família de
/// impressora deixa lá (csv/"printer2", xml, at-binário).
///
/// Console do Windows (nbtstat/ping/net view) responde na página de código OEM da máquina
/// (850 no Windows em pt-BR), não em UTF-8 — por isso o registro explícito do code page abaixo,
/// equivalente ao <c>iconv.decode(stdout, "cp850")</c> da referência.
/// </summary>
public sealed class VarreduraDeRedeService
{
    private const int PortaSmb = 445;
    private const int TimeoutDaPortaMs = 600;
    private const int ConcorrenciaDePortas = 64;
    private const int ConcorrenciaDeHosts = 6;
    private const int TimeoutDeArquivoMs = 4000;

    private static readonly string[] CompartilhamentosDeReserva =
        ["PrinterManager", "temp", "Users", "AT.printer_1.3", "AT.printer", "printer"];

    private static readonly (Regex Padrao, TipoDeMaquina Tipo)[] PrioridadeDeCompartilhamento =
    [
        (new Regex("^printermanager", RegexOptions.IgnoreCase), default),
        (new Regex("^temp$", RegexOptions.IgnoreCase), default),
        (new Regex("^at[._ ]?printer", RegexOptions.IgnoreCase), default),
        (new Regex("^users$", RegexOptions.IgnoreCase), default),
    ];

    static VarreduraDeRedeService()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    public async Task<(int Alcancaveis, List<MaquinaEncontrada> Achadas)> VarrerAsync(
        IReadOnlyList<string> hosts,
        IProgress<ProgressoDaVarredura>? progresso,
        CancellationToken ct = default)
    {
        List<(string? Host, string? Ip)> alcancaveis;

        if (hosts.Count > 0)
        {
            progresso?.Report(new ProgressoDaVarredura("hosts", 0, hosts.Count, $"Testando {hosts.Count} host(s) informado(s)..."));
            var checados = await MapLimitAsync(hosts, ConcorrenciaDePortas, async alvo =>
            {
                if (!await ProbePortaAsync(alvo, ct)) return ((string?)null, (string?)null);
                if (IPAddress.TryParse(alvo, out _)) return (null, alvo);
                try
                {
                    var entrada = await Dns.GetHostEntryAsync(alvo, ct);
                    var ipv4 = entrada.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                    return (alvo, ipv4?.ToString());
                }
                catch { return (alvo, null); }
            }, ct);
            alcancaveis = checados.Where(c => c.Item1 is not null || c.Item2 is not null).ToList();
        }
        else
        {
            var alvos = AlvosLocais();
            progresso?.Report(new ProgressoDaVarredura("sweep", 0, alvos.Count, $"Varrendo {alvos.Count} endereços na rede local..."));
            var escaneados = 0;
            var checados = await MapLimitAsync(alvos, ConcorrenciaDePortas, async ip =>
            {
                if (ct.IsCancellationRequested) return ((string?)null, (string?)null);
                var ok = await ProbePortaAsync(ip, ct);
                var n = Interlocked.Increment(ref escaneados);
                if (n % 25 == 0) progresso?.Report(new ProgressoDaVarredura("sweep", n, alvos.Count));
                return ok ? (null, ip) : (null, null);
            }, ct);
            progresso?.Report(new ProgressoDaVarredura("sweep", alvos.Count, alvos.Count));
            alcancaveis = checados.Where(c => c.Item2 is not null).ToList();
        }

        progresso?.Report(new ProgressoDaVarredura("identify", 0, alcancaveis.Count,
            $"{alcancaveis.Count} computador(es) com compartilhamento respondendo. Identificando..."));

        var identificados = 0;
        var resultados = await MapLimitAsync(alcancaveis, ConcorrenciaDeHosts, async entrada =>
        {
            if (ct.IsCancellationRequested) return null;

            var host = entrada.Host ?? await ResolverNomeDoHostAsync(entrada.Ip!, ct) ?? entrada.Ip!;
            var ip = entrada.Ip;
            MaquinaEncontrada? achada = null;

            try
            {
                var compartilhamentos = await ListarCompartilhamentosAsync(host, ct);
                if (compartilhamentos.Count > 0)
                    achada = await IdentificarHostAsync(host, ip, compartilhamentos, ct);
            }
            catch
            {
                achada = null;
            }

            var n = Interlocked.Increment(ref identificados);
            progresso?.Report(new ProgressoDaVarredura("identify", n, alcancaveis.Count,
                achada is not null ? $"{host}: {RotuloDoTipo(achada.Tipo)}" : null));

            return achada;
        }, ct);

        return (alcancaveis.Count, resultados.Where(r => r is not null).Select(r => r!).ToList());
    }

    public static string RotuloDoTipo(TipoDeMaquina tipo) => tipo switch
    {
        TipoDeMaquina.Csv => "PrinterManager (CSV)",
        TipoDeMaquina.Xml => "PrinterManager (XML)",
        TipoDeMaquina.AtBinario => "AT Printer (binário)",
        _ => tipo.ToString(),
    };

    /// <summary>Porte de <c>suggestIdentity</c> — o id sai do nome do computador (estável), o nome é só sugestão.</summary>
    public static (string Id, string Nome) SugerirIdentidade(string host, ISet<string> idsUsados)
    {
        var numerado = Regex.Match(host, @"impressora[^0-9]*(\d{1,2})", RegexOptions.IgnoreCase);
        string id, nome;
        if (numerado.Success)
        {
            var digitos = numerado.Groups[1].Value.PadLeft(2, '0');
            id = $"imp{digitos}";
            nome = $"Impressora {digitos}";
        }
        else
        {
            id = Regex.Replace(host.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (string.IsNullOrEmpty(id)) id = "maquina";
            nome = host;
        }

        var unico = id;
        var sufixo = 2;
        while (idsUsados.Contains(unico)) unico = $"{id}-{sufixo++}";
        return (unico, nome);
    }

    // ------------------------------------------------------------ alvos da rede

    private static List<string> AlvosLocais()
    {
        var alvos = new List<string>();
        var vistos = new HashSet<string>();

        foreach (var interfaceDeRede in NetworkInterface.GetAllNetworkInterfaces())
        {
            var props = interfaceDeRede.GetIPProperties();
            foreach (var unicast in props.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(unicast.Address)) continue;

                var partes = unicast.Address.ToString().Split('.');
                var baseIp = string.Join('.', partes.Take(3));
                if (!vistos.Add(baseIp)) continue;

                for (var host = 1; host <= 254; host++)
                {
                    var ip = $"{baseIp}.{host}";
                    if (ip != unicast.Address.ToString()) alvos.Add(ip);
                }
            }
        }

        return alvos;
    }

    private static async Task<bool> ProbePortaAsync(string ip, CancellationToken ct)
    {
        using var socket = new TcpClient();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeoutDaPortaMs);
            await socket.ConnectAsync(ip, PortaSmb, cts.Token);
            return socket.Connected;
        }
        catch
        {
            return false;
        }
    }

    // ------------------------------------------------------------ identidade (nome/rede)

    /// <summary>nbtstat -A, depois ping -a, depois DNS reverso — nessa ordem porque o nome NetBIOS sobrevive a troca de IP pelo DHCP.</summary>
    private static async Task<string?> ResolverNomeDoHostAsync(string ip, CancellationToken ct)
    {
        var nbt = await RodarComandoAsync("nbtstat", ["-A", ip], 5000, ct);
        string? unico = null;
        foreach (var linha in nbt.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var m = Regex.Match(linha, @"^\s*(\S+)\s*<(00|20)>\s+(\S+)");
            if (!m.Success || Regex.IsMatch(m.Groups[3].Value, "^(GROUP|GRUPO)", RegexOptions.IgnoreCase)) continue;
            if (m.Groups[2].Value == "20") return m.Groups[1].Value.Trim();
            unico ??= m.Groups[1].Value.Trim();
        }
        if (unico is not null) return unico;

        var pingado = await RodarComandoAsync("ping", ["-a", "-n", "1", "-w", "800", ip], 4000, ct);
        var nomeado = Regex.Match(pingado, $@"(\S+)\s*\[{Regex.Escape(ip)}\]");
        if (nomeado.Success && !IPAddress.TryParse(nomeado.Groups[1].Value, out _))
            return nomeado.Groups[1].Value.Split('.')[0];

        try
        {
            var entrada = await Dns.GetHostEntryAsync(ip, ct).WaitAsync(TimeSpan.FromSeconds(2), ct);
            if (!string.IsNullOrEmpty(entrada.HostName)) return entrada.HostName.Split('.')[0];
        }
        catch { /* sem DNS reverso — segue sem nome */ }

        return null;
    }

    private static async Task<List<string>> ListarCompartilhamentosAsync(string host, CancellationToken ct)
    {
        var saida = await RodarComandoAsync("net", ["view", $@"\\{host}", "/all"], 8000, ct);
        var compartilhamentos = new List<string>();
        var iniciou = false;
        foreach (var linha in saida.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (Regex.IsMatch(linha, "^-{5,}")) { iniciou = true; continue; }
            if (!iniciou) continue;
            if (string.IsNullOrWhiteSpace(linha)) break;

            var partes = Regex.Split(linha.Trim(), @"\s{2,}");
            var nome = partes.ElementAtOrDefault(0);
            var tipo = partes.ElementAtOrDefault(1) ?? "";
            if (string.IsNullOrEmpty(nome) || nome.EndsWith('$')) continue;
            if (!Regex.IsMatch(tipo, "^(Disk|Disco)", RegexOptions.IgnoreCase)) continue;
            compartilhamentos.Add(nome);
        }

        if (compartilhamentos.Count > 0) return compartilhamentos;

        // Sem enumeração (host bloqueia "net view"): testa a lista conhecida direto.
        var testados = await MapLimitAsync(CompartilhamentosDeReserva, 6, async share =>
            await EhPastaAsync(Unc(host, share)) ? share : null, ct);
        return testados.Where(s => s is not null).Select(s => s!).ToList();
    }

    // --------------------------------------------------------- impressões digitais

    private static async Task<MaquinaEncontrada?> IdentificarHostAsync(string host, string? ip, List<string> compartilhamentos, CancellationToken ct)
    {
        foreach (var share in OrdenarCompartilhamentos(compartilhamentos))
        {
            foreach (var raiz in await RaizesCandidatasAsync(host, share, ct))
            {
                // Ordem importa: o AT usa a pasta "PrintHistory", que o detector XML também
                // aceita. O binário é o teste mais específico, então vem antes.
                var achada = await DetectarCsvAsync(share, raiz, host, ip, ct)
                    ?? await DetectarAtBinarioAsync(share, raiz, host, ip, compartilhamentos, ct)
                    ?? await DetectarXmlAsync(share, raiz, host, ip, ct);
                if (achada is not null) return achada;
            }
        }
        return null;
    }

    private static IEnumerable<string> OrdenarCompartilhamentos(List<string> compartilhamentos) =>
        compartilhamentos
            .Select((nome, indice) => (nome, rank: PrioridadeDeCompartilhamento
                .Select((p, i) => p.Padrao.IsMatch(nome) ? i : -1)
                .Where(i => i >= 0)
                .DefaultIfEmpty(PrioridadeDeCompartilhamento.Length)
                .Min()))
            .OrderBy(x => x.rank)
            .Select(x => x.nome);

    private static async Task<List<string>> RaizesCandidatasAsync(string host, string share, CancellationToken ct)
    {
        var raizDoShare = Unc(host, share);
        var raizes = new List<string> { raizDoShare };

        if (await EhPastaAsync($@"{raizDoShare}\PrinterManager")) raizes.Add($@"{raizDoShare}\PrinterManager");

        var entradas = await LerPastaAsync(raizDoShare, ct);
        foreach (var pasta in entradas.Take(40))
        {
            var desktop = $@"{raizDoShare}\{pasta}\Desktop\PrinterManager";
            if (await EhPastaAsync(desktop)) raizes.Add(desktop);
        }

        return raizes;
    }

    private static async Task<MaquinaEncontrada?> DetectarCsvAsync(string share, string raiz, string host, string? ip, CancellationToken ct)
    {
        if (!await EhArquivoAsync($@"{raiz}\History.csv")) return null;

        var entradas = await LerPastaAsync(raiz, ct);
        var dllDeTinta = entradas.FirstOrDefault(n => Regex.IsMatch(n, "^ink.*\\.dll$", RegexOptions.IgnoreCase));

        return new MaquinaEncontrada
        {
            Host = host,
            Ip = ip,
            Tipo = TipoDeMaquina.Csv,
            Share = share,
            Raiz = raiz,
            CaminhoHistorico = $@"{raiz}\History.csv",
            PastaPreview = await EhPastaAsync($@"{raiz}\Preview") ? $@"{raiz}\Preview" : null,
            CaminhoListaDeTrabalhos = await EhArquivoAsync($@"{raiz}\Joblist.xml") ? $@"{raiz}\Joblist.xml" : null,
            CaminhoEstatisticasDeTinta = dllDeTinta is not null ? $@"{raiz}\{dllDeTinta}" : null,
            ArquivoLogAoVivo = await EhArquivoAsync($@"{raiz}\log.txt") ? $@"{raiz}\log.txt" : null,
            PastaLogDeStatus = await EhPastaAsync($@"{raiz}\Log") ? $@"{raiz}\Log" : null,
        };
    }

    /// <summary>"PrintHistroy" (com o erro de digitação do software original) só existe na versão XML; "PrintHistory" só conta como XML se tiver subpastas de ano (AAAA), senão é o binário AT.</summary>
    private static async Task<string?> PastaDeHistoricoXmlAsync(string raiz, CancellationToken ct)
    {
        if (await EhPastaAsync($@"{raiz}\PrintHistroy")) return $@"{raiz}\PrintHistroy";

        var alternativa = $@"{raiz}\PrintHistory";
        if (!await EhPastaAsync(alternativa)) return null;

        var entradas = await LerSubpastasAsync(alternativa, ct);
        return entradas.Any(nome => Regex.IsMatch(nome, @"^\d{4}$")) ? alternativa : null;
    }

    private static async Task<MaquinaEncontrada?> DetectarXmlAsync(string share, string raiz, string host, string? ip, CancellationToken ct)
    {
        var caminhoHistorico = await PastaDeHistoricoXmlAsync(raiz, ct);
        if (caminhoHistorico is null) return null;

        return new MaquinaEncontrada
        {
            Host = host,
            Ip = ip,
            Tipo = TipoDeMaquina.Xml,
            Share = share,
            Raiz = raiz,
            CaminhoHistorico = caminhoHistorico,
            PastaPreview = await EhPastaAsync($@"{raiz}\Preview") ? $@"{raiz}\Preview" : null,
            PastaLogAoVivo = await EhPastaAsync($@"{raiz}\Log") ? $@"{raiz}\Log" : null,
        };
    }

    private static async Task<MaquinaEncontrada?> DetectarAtBinarioAsync(string share, string raiz, string host, string? ip, List<string> compartilhamentos, CancellationToken ct)
    {
        var caminhoHistorico = $@"{raiz}\PrintHistory\PrintHistory";
        if (!await EhArquivoAsync(caminhoHistorico)) return null;

        string? pastaPreview = null;
        var shareAt = compartilhamentos.FirstOrDefault(nome => Regex.IsMatch(nome, "^at[._ ]?printer", RegexOptions.IgnoreCase));
        foreach (var candidata in new[] { shareAt is not null ? Unc(host, shareAt, "preview") : null, $@"{raiz}\preview" })
        {
            if (candidata is not null && await EhPastaAsync(candidata)) { pastaPreview = candidata; break; }
        }

        return new MaquinaEncontrada
        {
            Host = host,
            Ip = ip,
            Tipo = TipoDeMaquina.AtBinario,
            Share = share,
            Raiz = raiz,
            CaminhoHistorico = caminhoHistorico,
            PastaPreview = pastaPreview,
        };
    }

    // ------------------------------------------------------------------ utilidades

    private static string Unc(params string?[] partes) => @"\\" + string.Join('\\', partes.Where(p => !string.IsNullOrEmpty(p)));

    private static async Task<bool> EhArquivoAsync(string caminho) =>
        await ComTimeoutAsync(() => Task.Run(() => File.Exists(caminho)), TimeoutDeArquivoMs, false);

    private static async Task<bool> EhPastaAsync(string caminho) =>
        await ComTimeoutAsync(() => Task.Run(() => Directory.Exists(caminho)), TimeoutDeArquivoMs, false);

    private static async Task<List<string>> LerPastaAsync(string caminho, CancellationToken ct) =>
        await ComTimeoutAsync(() => Task.Run(() =>
        {
            try { return Directory.GetFileSystemEntries(caminho).Select(Path.GetFileName).Where(n => n is not null).Select(n => n!).ToList(); }
            catch { return []; }
        }, ct), TimeoutDeArquivoMs, []);

    private static async Task<List<string>> LerSubpastasAsync(string caminho, CancellationToken ct) =>
        await ComTimeoutAsync(() => Task.Run(() =>
        {
            try { return Directory.GetDirectories(caminho).Select(Path.GetFileName).Where(n => n is not null).Select(n => n!).ToList(); }
            catch { return []; }
        }, ct), TimeoutDeArquivoMs, []);

    private static async Task<T> ComTimeoutAsync<T>(Func<Task<T>> operacao, int timeoutMs, T valorPadrao)
    {
        var tarefa = operacao();
        var concluida = await Task.WhenAny(tarefa, Task.Delay(timeoutMs));
        return concluida == tarefa ? await tarefa : valorPadrao;
    }

    private static async Task<string> RodarComandoAsync(string comando, string[] args, int timeoutMs, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo(comando)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.GetEncoding(850),
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            using var processo = Process.Start(psi);
            if (processo is null) return "";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            var saida = new StringBuilder();
            var leitura = Task.Run(async () => saida.Append(await processo.StandardOutput.ReadToEndAsync(cts.Token)), cts.Token);

            try { await processo.WaitForExitAsync(cts.Token); await leitura; }
            catch (OperationCanceledException) { try { processo.Kill(true); } catch { /* já saiu */ } }

            return saida.ToString();
        }
        catch
        {
            return "";
        }
    }

    private static async Task<List<TResultado>> MapLimitAsync<TItem, TResultado>(
        IReadOnlyList<TItem> itens, int limite, Func<TItem, Task<TResultado>> trabalho, CancellationToken ct = default)
    {
        var resultados = new TResultado[itens.Count];
        var cursor = 0;
        var executores = Enumerable.Range(0, Math.Min(limite, Math.Max(itens.Count, 1))).Select(async _ =>
        {
            while (true)
            {
                var indice = Interlocked.Increment(ref cursor) - 1;
                if (indice >= itens.Count) break;
                resultados[indice] = await trabalho(itens[indice]);
            }
        });
        await Task.WhenAll(executores);
        return resultados.ToList();
    }
}
