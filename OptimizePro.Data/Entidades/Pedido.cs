namespace OptimizePro.Data.Entidades;

/// <summary>Porte de <c>imp_pedidos</c> (optmize-full, §22) — um lote de itens para a calandra.</summary>
public class Pedido
{
    public required string Id { get; set; }
    public DateTime CriadoEm { get; set; }
    public string Status { get; set; } = "aberto";
    public string? Observacao { get; set; }

    public List<PedidoItem> Itens { get; set; } = [];
}
