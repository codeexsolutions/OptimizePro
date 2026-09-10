namespace OptimizePro.Central;

public sealed class InstalacaoService(IInstalacaoRepository repositorio) : IInstalacaoService
{
    public async Task<ResultadoDoProvisionamento> ProvisionarAsync(uint clienteIdHash, string? nomeDaFabrica, CancellationToken ct = default)
    {
        var existente = await repositorio.ObterPorClienteIdHashAsync(clienteIdHash, ct);
        if (existente is not null)
            return new ResultadoDoProvisionamento(existente, null, JaExistia: true);

        var chave = ChaveDeApi.Gerar();
        var codigo = await GerarCodigoUnicoAsync(ct);
        var instalacao = new Instalacao
        {
            Id = "",
            Codigo = codigo,
            ClienteIdHash = clienteIdHash,
            ChaveDeApiHash = ChaveDeApi.Hash(chave),
            NomeDaFabrica = nomeDaFabrica,
        };

        var criada = await repositorio.CriarAsync(instalacao, ct);
        return new ResultadoDoProvisionamento(criada, chave, JaExistia: false);
    }

    private async Task<string> GerarCodigoUnicoAsync(CancellationToken ct)
    {
        // Colisão é praticamente impossível (32^6 ≈ 1 bilhão de combinações), mas um retry
        // curto é mais barato que confiar cegamente nisso.
        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            var codigo = CodigoDaInstalacao.Gerar();
            if (await repositorio.ObterPorCodigoAsync(codigo, ct) is null) return codigo;
        }
        throw new InvalidOperationException("Não consegui gerar um código de instalação único.");
    }

    public async Task<Instalacao?> AutenticarAsync(string instalacaoId, string chaveDeApi, CancellationToken ct = default)
    {
        var instalacao = await repositorio.ObterAsync(instalacaoId, ct);
        if (instalacao is null || !ChaveDeApi.Conferir(chaveDeApi, instalacao.ChaveDeApiHash))
            return null;

        return instalacao;
    }

    public Task RegistrarSincronizacaoAsync(string instalacaoId, CancellationToken ct = default) =>
        repositorio.RegistrarSincronizacaoAsync(instalacaoId, DateTime.UtcNow, ct);
}
