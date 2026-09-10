namespace OptimizePro.Data.Entidades;

/// <summary>Porte de <c>imp_records</c> (optmize-full, §22) — uma linha do histórico de uma máquina.</summary>
public class RegistroDeImpressao
{
    public required string Id { get; set; }
    public required string MaquinaId { get; set; }
    public string? NomeDaMaquina { get; set; }
    public string? TipoDeOrigem { get; set; }

    public required string DataHora { get; set; }
    public required string Data { get; set; }
    public string? Hora { get; set; }
    public string? Tarefa { get; set; }
    public int? Passada { get; set; }
    public string? Status { get; set; }

    public bool Cancelada { get; set; }
    public bool ComErro { get; set; }
    public double AreaDeImpressao { get; set; }
    public double ComprimentoDeImpressao { get; set; }

    public bool MetricaEstimada { get; set; }
    public double? Concluido { get; set; }
    public double? Total { get; set; }
    public int TempoSegundos { get; set; }

    public double TintaMl { get; set; }
    public bool TintaExperimental { get; set; }
    public Dictionary<string, double>? CanaisDeTinta { get; set; }
    public string? ReferenciaDePreview { get; set; }

    public double? PercentualDeProgresso { get; set; }
    public string? EstadoDoProgresso { get; set; }
    public double? HorasDecorridas { get; set; }
    public bool EhRecorteOuMosaico { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public Maquina? Maquina { get; set; }
}
