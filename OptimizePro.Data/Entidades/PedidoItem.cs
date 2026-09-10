namespace OptimizePro.Data.Entidades;

/// <summary>
/// Porte de <c>imp_pedido_items</c> (optmize-full, §22). <see cref="RegistroId"/> é uma
/// referência solta (sem FK), igual à referência — o registro de origem pode já ter
/// rolado do histórico quando o item da calandra ainda está pendente.
/// </summary>
public class PedidoItem
{
    public required string Id { get; set; }
    public required string PedidoId { get; set; }
    public int Posicao { get; set; }
    public required string RegistroId { get; set; }
    public string? NomeDoCliente { get; set; }
    public string? Tecido { get; set; }
    public string? Tarefa { get; set; }
    public string? MaquinaId { get; set; }
    public string? NomeDaMaquina { get; set; }
    public double? ComprimentoDeImpressao { get; set; }
    public string? Data { get; set; }

    public string? OrdemId { get; set; }
    public string StatusNaCalandra { get; set; } = "pendente";
    public string? MotivoNaCalandra { get; set; }
    public string? MotivoPersonalizadoNaCalandra { get; set; }
    public DateTime? DataNaCalandra { get; set; }

    public Pedido? Pedido { get; set; }
    public OrdemDeServico? Ordem { get; set; }
}
