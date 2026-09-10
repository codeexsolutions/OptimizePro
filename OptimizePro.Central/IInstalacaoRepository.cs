namespace OptimizePro.Central;

public interface IInstalacaoRepository
{
    Task<Instalacao?> ObterPorClienteIdHashAsync(uint clienteIdHash, CancellationToken ct = default);

    Task<Instalacao?> ObterAsync(string id, CancellationToken ct = default);

    Task<Instalacao?> ObterPorCodigoAsync(string codigo, CancellationToken ct = default);

    Task<Instalacao> CriarAsync(Instalacao instalacao, CancellationToken ct = default);

    Task RegistrarSincronizacaoAsync(string id, DateTime quando, CancellationToken ct = default);
}
