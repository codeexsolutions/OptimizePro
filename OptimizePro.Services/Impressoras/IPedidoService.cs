using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Services.Impressoras;

/// <summary>Um trabalho do Histórico marcado pra entrar num pedido (§22.8) — o que a tela manda, antes de virar <see cref="PedidoItem"/>.</summary>
public sealed record ItemParaPedido(string RegistroId, string? Tarefa, string? MaquinaId, string? NomeDaMaquina, double? ComprimentoDeImpressao, string? Data);

/// <summary>Fachada da fila da calandra (§22.8, porte de <c>routes/pedidos.js</c>).</summary>
public interface IPedidoService
{
    Task<string> CriarAsync(IReadOnlyList<ItemParaPedido> itens, string? observacao, CancellationToken ct = default);
    Task<List<ResumoDoPedido>> ListarAsync(CancellationToken ct = default);
    Task<Pedido?> ObterAsync(string id, CancellationToken ct = default);
    Task<bool> ExcluirAsync(string id, CancellationToken ct = default);
    Task<bool> AtualizarStatusAsync(string id, string status, CancellationToken ct = default);
    Task<PedidoItem?> AtualizarResultadoDoItemAsync(string pedidoId, string itemId, string status, string? motivo, string? motivoPersonalizado, CancellationToken ct = default);
}
