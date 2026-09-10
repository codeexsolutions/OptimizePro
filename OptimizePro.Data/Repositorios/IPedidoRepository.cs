using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

/// <summary>Resumo de um pedido pra lista (porte de <c>listPedidos</c>, §22.8) — conta itens sem carregar o pedido inteiro.</summary>
public sealed record ResumoDoPedido(string Id, DateTime CriadoEm, string Status, string? Observacao, int QuantidadeDeItens, int QuantidadeOk, int QuantidadeComErro);

/// <summary>Espelha <c>impressoras/db/pedidos.js</c> (§22.8) — a fila da calandra.</summary>
public interface IPedidoRepository
{
    /// <summary>Cria o pedido com os itens já na posição de entrada. Retorna o id gerado.</summary>
    Task<string> CriarAsync(IReadOnlyList<PedidoItem> itens, string? observacao, CancellationToken ct = default);

    Task<List<ResumoDoPedido>> ListarAsync(CancellationToken ct = default);

    Task<Pedido?> ObterAsync(string id, CancellationToken ct = default);

    Task<bool> ExcluirAsync(string id, CancellationToken ct = default);

    Task<bool> AtualizarStatusAsync(string id, string status, CancellationToken ct = default);

    /// <summary>Chamado pelo app da calandra depois que o operador marca certo/errado. Retorna null se o item não existir ou não pertencer ao pedido.</summary>
    Task<PedidoItem?> AtualizarResultadoDoItemAsync(string pedidoId, string itemId, string status, string? motivo, string? motivoPersonalizado, CancellationToken ct = default);

    /// <summary>Porte de <c>findItemByRecordId</c> — usado pelo endpoint de scan pra achar o item de pedido a partir do registro de origem.</summary>
    Task<PedidoItem?> ObterPorRegistroIdAsync(string registroId, CancellationToken ct = default);
}
