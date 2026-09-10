using Microsoft.Extensions.DependencyInjection;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Services.Impressoras;

/// <summary>
/// Dono do estado da varredura em andamento (porte do objeto <c>scan</c> de
/// <c>routes/machines.js</c>, §22) — precisa ser singleton porque a varredura leva uns bons
/// segundos e tem que sobreviver além do escopo da requisição/tela que a disparou, com
/// qualquer outra tela conseguindo perguntar "como está indo" enquanto isso.
///
/// Cria seu próprio escopo de DI (<see cref="IServiceScopeFactory"/>) pra falar com o banco,
/// já que o <c>OptimizeDbContext</c> é scoped e a varredura roda solta, fora de qualquer
/// escopo de tela.
/// </summary>
public sealed class GerenciadorDeVarredura(IServiceScopeFactory scopeFactory, VarreduraDeRedeService varredura)
{
    private readonly Lock _trava = new();
    private readonly Dictionary<string, MaquinaEncontrada> _pendentes = [];
    private EstadoDaVarredura _estado = new();
    private CancellationTokenSource? _cts;

    /// <summary>Disparado a cada atualização de progresso — equivalente ao <c>io.emit("machines:scan", ...)</c> da referência (quem escuta hoje é a tela; o hub SignalR entra quando o painel remoto existir).</summary>
    public event Action<EstadoDaVarredura>? Atualizado;

    public EstadoDaVarredura ObterEstado()
    {
        lock (_trava) return _estado.Clonar();
    }

    public MaquinaEncontrada? ObterPendente(string host)
    {
        lock (_trava) return _pendentes.GetValueOrDefault(host);
    }

    /// <summary>Retorna false se já existe uma varredura rodando — só uma por vez, senão disputam a rede.</summary>
    public bool Iniciar(IReadOnlyList<string> hosts)
    {
        lock (_trava)
        {
            if (_estado.EmExecucao) return false;
            _cts = new CancellationTokenSource();
            _estado = new EstadoDaVarredura { EmExecucao = true, Fase = "starting", Mensagem = "Iniciando varredura..." };
        }
        Publicar();
        _ = ExecutarAsync(hosts, _cts.Token);
        return true;
    }

    public void Parar()
    {
        lock (_trava) _cts?.Cancel();
    }

    public bool Descartar(string host)
    {
        lock (_trava)
        {
            if (!_pendentes.Remove(host)) return false;
            _estado.Resultados.RemoveAll(r => r.Host == host);
        }
        Publicar();
        return true;
    }

    private void RemoverPendente(string host)
    {
        lock (_trava) _pendentes.Remove(host);
    }

    public void RegistrarCadastro(string host, string maquinaId, string maquinaNome)
    {
        lock (_trava)
        {
            _pendentes.Remove(host);
            var indice = _estado.Resultados.FindIndex(r => r.Host == host);
            if (indice >= 0)
                _estado.Resultados[indice] = _estado.Resultados[indice] with { Acao = "cadastrada", MaquinaId = maquinaId, MaquinaNome = maquinaNome };
        }
        Publicar();
    }

    private async Task ExecutarAsync(IReadOnlyList<string> hosts, CancellationToken ct)
    {
        var progresso = new Progress<ProgressoDaVarredura>(AtualizarProgresso);
        try
        {
            var (alcancaveis, achadas) = await varredura.VarrerAsync(hosts, progresso, ct);

            using var escopo = scopeFactory.CreateScope();
            var repo = escopo.ServiceProvider.GetRequiredService<IMaquinaRepository>();
            var existentes = await repo.ListarAsync(incluirDesabilitadas: true, ct);

            var resultados = new List<ResultadoDaVarredura>();
            foreach (var achada in achadas)
            {
                var existente = existentes.FirstOrDefault(m => m.Host == achada.Host)
                    ?? (achada.Ip is not null ? existentes.FirstOrDefault(m => m.Ip == achada.Ip) : null);

                if (existente is not null)
                {
                    existente.Tipo = achada.Tipo;
                    existente.Host = achada.Host;
                    existente.Ip = achada.Ip ?? existente.Ip;
                    existente.CaminhoHistorico = achada.CaminhoHistorico;
                    existente.PastaPreview = achada.PastaPreview;
                    existente.PastaLogAoVivo = achada.PastaLogAoVivo;
                    existente.ArquivoLogAoVivo = achada.ArquivoLogAoVivo;
                    existente.PastaLogDeStatus = achada.PastaLogDeStatus;
                    existente.CaminhoListaDeTrabalhos = achada.CaminhoListaDeTrabalhos;
                    existente.CaminhoEstatisticasDeTinta = achada.CaminhoEstatisticasDeTinta;
                    existente.AtualizadoEm = DateTime.UtcNow;
                    await repo.SalvarAsync(existente, ct);
                    resultados.Add(new ResultadoDaVarredura("atualizada", achada.Host, achada.Ip, achada.Tipo,
                        VarreduraDeRedeService.RotuloDoTipo(achada.Tipo), achada.Share, achada.Raiz, existente.Id, existente.Nome));
                }
                else
                {
                    lock (_trava) _pendentes[achada.Host] = achada;
                    resultados.Add(new ResultadoDaVarredura("pendente", achada.Host, achada.Ip, achada.Tipo,
                        VarreduraDeRedeService.RotuloDoTipo(achada.Tipo), achada.Share, achada.Raiz, null, null));
                }
            }

            var novas = resultados.Count(r => r.Acao == "pendente");
            var atualizadas = resultados.Count(r => r.Acao == "atualizada");

            lock (_trava)
            {
                _estado.Fase = "done";
                _estado.Alcancaveis = alcancaveis;
                _estado.Resultados = resultados;
                _estado.Mensagem = resultados.Count > 0
                    ? $"{resultados.Count} máquina(s) identificada(s)" +
                      (novas > 0 ? $" — {novas} nova(s) esperando você dar o nome" : "") +
                      $" — {atualizadas} atualizada(s)."
                    : $"Nenhuma impressora reconhecida entre os {alcancaveis} computador(es) que responderam.";
            }
        }
        catch (OperationCanceledException)
        {
            lock (_trava) { _estado.Fase = "cancelada"; _estado.Mensagem = "Varredura cancelada."; }
        }
        catch (Exception ex)
        {
            lock (_trava) { _estado.Fase = "error"; _estado.Erro = ex.Message; _estado.Mensagem = $"Falha na varredura: {ex.Message}"; }
        }
        finally
        {
            lock (_trava) _estado.EmExecucao = false;
            Publicar();
        }
    }

    private void AtualizarProgresso(ProgressoDaVarredura p)
    {
        lock (_trava)
        {
            _estado.Fase = p.Fase;
            _estado.Escaneados = p.Escaneados;
            _estado.Total = p.Total;
            if (p.Mensagem is not null) _estado.Mensagem = p.Mensagem;
        }
        Publicar();
    }

    private void Publicar() => Atualizado?.Invoke(ObterEstado());
}
