using System.Net.Http.Json;

namespace OptimizePro.Sincronizacao;

/// <summary>
/// Onde a Central está hospedada (§24) — via variável de ambiente
/// (<c>OPTIMIZE_CENTRAL_URL</c>) porque isso muda entre "testando local" (o
/// <c>dotnet run</c> do <c>OptimizePro.Central</c> em <c>http://localhost:5099</c>, usado pra
/// testar contra o Supabase) e produção (URL pública real, ainda não decidida/hospedada —
/// hospedagem é responsabilidade do usuário, fora do que este ambiente de desenvolvimento
/// provisiona). Sem a variável, sincronização simplesmente não acontece (fail-open).
/// </summary>
public sealed record ConfiguracaoDaCentral(string? UrlBase)
{
    public static ConfiguracaoDaCentral DoAmbiente() => new(Environment.GetEnvironmentVariable("OPTIMIZE_CENTRAL_URL"));
}

public sealed record RespostaDeProvisionamento(string InstalacaoId, string? ChaveDeApi, bool JaExistia);

/// <summary>Extraído como interface só pra <see cref="SincronizacaoService"/> ser testável sem precisar de um servidor HTTP de verdade nos testes.</summary>
public interface IClienteCentralHttp
{
    bool Configurado { get; }
    Task<RespostaDeProvisionamento?> ProvisionarAsync(uint clienteIdHash, string? nomeDaFabrica, CancellationToken ct = default);
    Task<bool> EnviarLoteAsync(string instalacaoId, string chaveDeApi, IReadOnlyList<ItemParaSincronizar> itens, CancellationToken ct = default);

    /// <summary>Puxa o estado atual de Usuario da Central (§25 — login/módulos no desktop). Null = falhou (offline, Central fora do ar); quem chama mantém o cache local antigo nesse caso.</summary>
    Task<List<UsuarioDto>?> ObterUsuariosAsync(string instalacaoId, string chaveDeApi, CancellationToken ct = default);
}

/// <summary>Fala HTTP com a Central (§24.2) — todo mundo aqui é best-effort: sem internet ou com a Central fora do ar, devolve null/false em vez de lançar, porque sincronização nunca pode travar o app desktop.</summary>
public sealed class ClienteCentralHttp : IClienteCentralHttp, IDisposable
{
    private readonly HttpClient? _http;

    public bool Configurado => _http is not null;

    public ClienteCentralHttp(ConfiguracaoDaCentral configuracao)
    {
        if (string.IsNullOrWhiteSpace(configuracao.UrlBase)) return;

        _http = new HttpClient { BaseAddress = new Uri(configuracao.UrlBase), Timeout = TimeSpan.FromSeconds(20) };
    }

    public async Task<RespostaDeProvisionamento?> ProvisionarAsync(uint clienteIdHash, string? nomeDaFabrica, CancellationToken ct = default)
    {
        if (_http is null) return null;

        try
        {
            var resposta = await _http.PostAsJsonAsync("/api/instalacoes/provisionar",
                new { clienteIdHash, nomeDaFabrica }, ct);
            if (!resposta.IsSuccessStatusCode) return null;

            return await resposta.Content.ReadFromJsonAsync<RespostaDeProvisionamento>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null; // offline, DNS, timeout — tenta de novo na próxima sincronização.
        }
    }

    public async Task<bool> EnviarLoteAsync(string instalacaoId, string chaveDeApi, IReadOnlyList<ItemParaSincronizar> itens, CancellationToken ct = default)
    {
        if (_http is null || itens.Count == 0) return false;

        try
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/sync/lote");
            requisicao.Headers.Add("X-Instalacao-Id", instalacaoId);
            requisicao.Headers.Add("X-Chave-Api", chaveDeApi);
            requisicao.Content = JsonContent.Create(new { itens });

            var resposta = await _http.SendAsync(requisicao, ct);
            return resposta.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    public async Task<List<UsuarioDto>?> ObterUsuariosAsync(string instalacaoId, string chaveDeApi, CancellationToken ct = default)
    {
        if (_http is null) return null;

        try
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/sync/usuarios");
            requisicao.Headers.Add("X-Instalacao-Id", instalacaoId);
            requisicao.Headers.Add("X-Chave-Api", chaveDeApi);

            var resposta = await _http.SendAsync(requisicao, ct);
            if (!resposta.IsSuccessStatusCode) return null;

            return await resposta.Content.ReadFromJsonAsync<List<UsuarioDto>>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public void Dispose() => _http?.Dispose();
}
