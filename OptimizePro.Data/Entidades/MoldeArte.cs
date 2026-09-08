using OptimizePro.Core.Arte;

namespace OptimizePro.Data.Entidades;

/// <summary>Tabela <c>molde_artes</c> (§3.3) — uma estampa (jogo de artes por papel de peça).</summary>
public class MoldeArte
{
    public int Id { get; set; }
    public int MoldeId { get; set; }
    public Molde? Molde { get; set; }

    public required string Nome { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public List<MoldeArtePeca> Pecas { get; set; } = [];
}

/// <summary>Tabela <c>molde_arte_pecas</c> (§3.4).</summary>
public class MoldeArtePeca
{
    public int Id { get; set; }
    public int ArteId { get; set; }
    public MoldeArte? Arte { get; set; }

    /// <summary>Parte da peça que esta arte cobre.</summary>
    public required string Papel { get; set; }

    /// <summary>Nome físico em <c>uploads/artes-molde</c>.</summary>
    public required string Arquivo { get; set; }

    public string? NomeOriginal { get; set; }

    /// <summary>JSON — mapeado como <see cref="AjusteArte"/> via value converter (§5.2).</summary>
    public AjusteArte? Ajuste { get; set; }
}
