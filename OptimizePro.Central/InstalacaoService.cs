namespace OptimizePro.Central;

public sealed class InstalacaoService(IInstalacaoRepository repositorio, IChaveDeMaquinaRepository chaves) : IInstalacaoService
{
    public async Task<ResultadoDoProvisionamento> ProvisionarAsync(uint clienteIdHash, string maquinaId, string? nomeDaFabrica, CancellationToken ct = default)
    {
        var instalacao = await repositorio.ObterPorClienteIdHashAsync(clienteIdHash, ct);
        var instalacaoJaExistia = instalacao is not null;

        if (instalacao is null)
        {
            var codigo = await GerarCodigoUnicoAsync(ct);
            instalacao = await repositorio.CriarAsync(new Instalacao
            {
                Id = "",
                Codigo = codigo,
                ClienteIdHash = clienteIdHash,
                NomeDaFabrica = nomeDaFabrica,
            }, ct);
        }

        // Várias máquinas da mesma gráfica compartilham a instalação acima (mesmo
        // ClienteIdHash), mas cada MaquinaId ainda precisa da própria chave — sem isso só a
        // primeira máquina a provisionar conseguiria sincronizar.
        if (await chaves.ObterAsync(instalacao.Id, maquinaId, ct) is not null)
            return new ResultadoDoProvisionamento(instalacao, null, instalacaoJaExistia);

        var chave = ChaveDeApi.Gerar();
        await chaves.CriarAsync(new ChaveDeMaquina
        {
            InstalacaoId = instalacao.Id,
            MaquinaId = maquinaId,
            ChaveHash = ChaveDeApi.Hash(chave),
        }, ct);

        return new ResultadoDoProvisionamento(instalacao, chave, instalacaoJaExistia);
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
        if (instalacao is null) return null;

        var chavesDaInstalacao = await chaves.ListarDaInstalacaoAsync(instalacaoId, ct);
        var alguma = chavesDaInstalacao.Any(c => ChaveDeApi.Conferir(chaveDeApi, c.ChaveHash));
        return alguma ? instalacao : null;
    }

    public Task RegistrarSincronizacaoAsync(string instalacaoId, CancellationToken ct = default) =>
        repositorio.RegistrarSincronizacaoAsync(instalacaoId, DateTime.UtcNow, ct);
}
