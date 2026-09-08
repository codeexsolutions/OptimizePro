namespace OptimizePro.Core.Vetor;

public abstract record SegmentoDeCaminho;

public sealed record SegmentoReta(PontoXY Fim) : SegmentoDeCaminho;

/// <summary>Arco de círculo (comando <c>A</c> do SVG, sempre rx=ry=Raio, sem rotação de eixo).</summary>
public sealed record SegmentoArco(double Raio, bool GrandeArco, bool Horario, PontoXY Fim) : SegmentoDeCaminho;

/// <summary>Bézier cúbica (comando <c>C</c> do SVG).</summary>
public sealed record SegmentoBezier(PontoXY Controle1, PontoXY Controle2, PontoXY Fim) : SegmentoDeCaminho;

public sealed record CaminhoMontado(PontoXY Inicio, IReadOnlyList<SegmentoDeCaminho> Segmentos);

/// <summary>
/// Parâmetros da tabela de §14.1: <see cref="ToleranciaDeReta"/>/<see cref="ToleranciaDeArco"/>
/// vêm de "suavidade"; <see cref="TensaoDeBezier"/> é "tensao" (0-2).
/// </summary>
public sealed record ParametrosDeRemontagem(
    double ToleranciaDeReta,
    double ToleranciaDeArco,
    double TensaoDeBezier = 1.0);
