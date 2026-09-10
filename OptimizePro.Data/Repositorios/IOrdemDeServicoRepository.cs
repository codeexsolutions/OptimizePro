using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

/// <summary>Resumo de uma OS pra lista (porte de <c>listOrders</c>, §22.9) — sem carregar os bytes das imagens.</summary>
public sealed record ResumoDeOrdemDeServico(string Id, string NomeDoCliente, string? Tecido, string Data, double? Metros, int QuantidadeDeImagens);

/// <summary>Espelha <c>impressoras/db/serviceOrders.js</c> (§22.9) — decisão do usuário: tela completa (criar+excluir), diferente da referência (só modelo de dados, sem tela).</summary>
public interface IOrdemDeServicoRepository
{
    Task<string> CriarAsync(OrdemDeServico ordem, CancellationToken ct = default);

    Task<List<ResumoDeOrdemDeServico>> ListarAsync(string? busca = null, CancellationToken ct = default);

    Task<OrdemDeServico?> ObterAsync(string id, CancellationToken ct = default);

    Task<(byte[] Dados, string TipoMime)?> ObterImagemAsync(string ordemId, string imagemId, CancellationToken ct = default);

    Task<bool> ExcluirAsync(string id, CancellationToken ct = default);
}
