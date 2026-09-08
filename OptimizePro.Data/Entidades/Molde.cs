using OptimizePro.Core;

namespace OptimizePro.Data.Entidades;

/// <summary>Tabela <c>moldes</c> (§3.1).</summary>
public class Molde
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public string? Observacoes { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public List<MoldePeca> Pecas { get; set; } = [];
    public List<MoldeArte> Artes { get; set; } = [];
}

/// <summary>Tabela <c>molde_pecas</c> (§3.2).</summary>
public class MoldePeca
{
    public int Id { get; set; }
    public int MoldeId { get; set; }
    public Molde? Molde { get; set; }

    public string Tamanho { get; set; } = "único";

    /// <summary>frente, costas, manga direita, manga esquerda, manga, gola, punho, cós, bolso, vista, forro, outro.</summary>
    public required string Papel { get; set; }

    public string? Nome { get; set; }
    public int Quantidade { get; set; } = 1;
    public double Largura { get; set; }
    public double Altura { get; set; }

    /// <summary>JSON — ≥3 pontos (§3.2). Mapeado como <see cref="List{PontoXY}"/> via value converter (§5.2).</summary>
    public List<PontoXY> Contorno { get; set; } = [];

    /// <summary>JSON — lista de furos (cada um sua lista de pontos), ou nulo.</summary>
    public List<List<PontoXY>>? Furos { get; set; }

    /// <summary>Ex.: "DXF · mm".</summary>
    public string? Origem { get; set; }

    public int Ordem { get; set; }
}
