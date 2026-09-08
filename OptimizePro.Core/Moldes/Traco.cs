namespace OptimizePro.Core.Moldes;

/// <summary>Um traço solto extraído de um arquivo (DXF/PLT/SVG/PDF), antes da costura em laços.</summary>
public sealed record Traco(IReadOnlyList<PontoXY> Pontos, bool Fechada);

/// <summary>Um rótulo de texto do arquivo, usado para nomear peças por proximidade/contenção.</summary>
public sealed record RotuloTexto(string Texto, double X, double Y);
