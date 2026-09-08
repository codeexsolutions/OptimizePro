namespace OptimizePro.Services.Moldes;

/// <summary>Substitui <c>moldes-api.js</c> (§4.2 da especificação) — cada método corresponde a uma rota antiga, chamada in-process (§6.1 da arquitetura).</summary>
public interface IMoldeService
{
    IReadOnlyList<string> ListarPapeis();

    Task<IReadOnlyList<MoldeResumo>> ListarAsync(CancellationToken ct = default);

    /// <exception cref="KeyNotFoundException">Molde não existe.</exception>
    Task<MoldeDetalhado> ObterAsync(int id, CancellationToken ct = default);

    /// <exception cref="ArgumentException">Nenhuma peça válida em <paramref name="entrada"/> (equivalente ao 400 do endpoint original).</exception>
    Task<int> CriarAsync(MoldeEntrada entrada, CancellationToken ct = default);

    /// <exception cref="ArgumentException">Nenhuma peça válida.</exception>
    /// <exception cref="KeyNotFoundException">Molde não existe.</exception>
    Task AtualizarAsync(int id, MoldeEntrada entrada, CancellationToken ct = default);

    Task ExcluirAsync(int id, CancellationToken ct = default);

    Task<string> SalvarImagemDeArteAsync(int moldeId, string papel, byte[] bytes, string? contentType, CancellationToken ct = default);

    Task<IReadOnlyList<EstampaDto>> ListarEstampasAsync(int moldeId, CancellationToken ct = default);

    /// <exception cref="KeyNotFoundException">Molde não existe, ou <c>entrada.Id</c> informado não pertence a este molde.</exception>
    Task<int> SalvarEstampaAsync(int moldeId, EstampaEntrada entrada, CancellationToken ct = default);

    Task ExcluirEstampaAsync(int moldeId, int arteId, CancellationToken ct = default);
}
