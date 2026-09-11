namespace OptimizePro.Central;

public interface IInstalacaoRepository
{
    Task<Instalacao?> ObterPorClienteIdHashAsync(uint clienteIdHash, CancellationToken ct = default);

    Task<Instalacao?> ObterAsync(string id, CancellationToken ct = default);

    Task<Instalacao?> ObterPorCodigoAsync(string codigo, CancellationToken ct = default);

    /// <summary>Só pro painel de staff (§26) — a Codeex Solutions vendo todos os clientes juntos; nenhum outro chamador deveria listar "todas" (cada instalação só enxerga a si mesma).</summary>
    Task<List<Instalacao>> ListarTodasAsync(CancellationToken ct = default);

    Task<Instalacao> CriarAsync(Instalacao instalacao, CancellationToken ct = default);

    Task RegistrarSincronizacaoAsync(string id, DateTime quando, CancellationToken ct = default);
}
