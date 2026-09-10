namespace OptimizePro.Data.Entidades;

/// <summary>Porte de <c>imp_service_order_images</c> (optmize-full, §22).</summary>
public class OrdemDeServicoImagem
{
    public required string Id { get; set; }
    public required string OrdemId { get; set; }
    public string? NomeDoArquivo { get; set; }
    public required string TipoMime { get; set; }
    public int Posicao { get; set; }
    public bool EhBlusa { get; set; }
    public int? Quantidade { get; set; }
    public required byte[] Dados { get; set; }

    public OrdemDeServico? Ordem { get; set; }
}
