namespace OptimizePro.Data.Entidades;

/// <summary>
/// Porte de <c>imp_service_orders</c> (optmize-full, §22). Ao contrário da referência
/// (que trata isso como só o modelo de dados, sem tela), aqui vira tela completa —
/// com criar e excluir ordem (decisão do usuário).
/// </summary>
public class OrdemDeServico
{
    public required string Id { get; set; }
    public required string NomeDoCliente { get; set; }
    public string? Tecido { get; set; }
    public string? TamanhoDeImpressao { get; set; }
    public double? Metros { get; set; }
    public string? Operador { get; set; }
    public string? Maquina { get; set; }
    public required string Data { get; set; }
    public string? Observacao { get; set; }
    public DateTime CriadoEm { get; set; }

    public List<OrdemDeServicoImagem> Imagens { get; set; } = [];
}
