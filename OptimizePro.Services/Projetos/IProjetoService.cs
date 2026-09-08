namespace OptimizePro.Services.Projetos;

/// <summary>Substitui <c>projetos-api.js</c> (§4.3 da especificação) — CRUD de cliente/projeto/peça (§6.2 da arquitetura).</summary>
public interface IProjetoService
{
    Task<IReadOnlyList<ClienteResumo>> ListarClientesAsync(CancellationToken ct = default);

    /// <exception cref="ArgumentException">Nome vazio/&gt;120 chars, ou observações &gt;500 chars.</exception>
    Task<int> CriarClienteAsync(ClienteEntrada entrada, CancellationToken ct = default);

    /// <exception cref="ArgumentException">Nome vazio/&gt;120 chars, ou observações &gt;500 chars.</exception>
    /// <exception cref="KeyNotFoundException">Cliente não existe.</exception>
    Task AtualizarClienteAsync(int id, ClienteEntrada entrada, CancellationToken ct = default);

    Task ExcluirClienteAsync(int id, CancellationToken ct = default);

    /// <exception cref="KeyNotFoundException">Cliente não existe.</exception>
    Task<ClienteComProjetos> ListarProjetosDoClienteAsync(int clienteId, CancellationToken ct = default);

    /// <exception cref="KeyNotFoundException">Projeto não existe.</exception>
    Task<ProjetoDetalhado> ObterProjetoAsync(int id, CancellationToken ct = default);

    Task<int> CriarProjetoAsync(int clienteId, string nome, CancellationToken ct = default);

    /// <exception cref="KeyNotFoundException">Projeto não existe.</exception>
    Task AtualizarProjetoAsync(int id, ProjetoEntrada entrada, CancellationToken ct = default);

    Task ExcluirProjetoAsync(int id, CancellationToken ct = default);

    /// <summary>Grava só a coluna miniatura de cada peça informada (§4.3). Retorna quantas foram de fato gravadas (ids que pertencem ao projeto, dentro do limite de tamanho).</summary>
    Task<int> PatchMiniaturasAsync(int projetoId, IReadOnlyList<MiniaturaEntrada> miniaturas, CancellationToken ct = default);

    /// <summary>Sem fallback por content-type — só assinatura binária (§4.3, diferente de <c>IMoldeService.SalvarImagemDeArteAsync</c>).</summary>
    Task<string> SalvarImagemDeProjetoAsync(byte[] bytes, CancellationToken ct = default);
}
