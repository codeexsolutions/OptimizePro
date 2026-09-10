using OptimizePro.Data.Entidades;

namespace OptimizePro.Services.Impressoras.Historico;

/// <summary>Porte dos totais do topo da tela de Histórico (<c>Historico.tsx</c>'s "summary").</summary>
public sealed record ResumoDoHistorico(int Trabalhos, int Concluidos, int Cancelados, double MetragemTotal, int TempoSegundosTotal, double TintaMlTotal)
{
    public static ResumoDoHistorico DeRegistros(IReadOnlyList<RegistroDeImpressao> registros) => new(
        Trabalhos: registros.Count,
        Concluidos: registros.Count(r => !r.Cancelada && !r.ComErro),
        Cancelados: registros.Count(r => r.Cancelada),
        MetragemTotal: registros.Sum(r => r.ComprimentoDeImpressao),
        TempoSegundosTotal: registros.Sum(r => r.TempoSegundos),
        TintaMlTotal: registros.Sum(r => r.TintaMl));
}
