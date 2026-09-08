namespace OptimizePro.Core.Encaixe;

/// <summary>Um retângulo livre do rolo (cm), usado pelo <see cref="EncaixadorPorCaixa"/> (MaxRects).</summary>
public readonly record struct RetanguloLivre(double X, double Y, double Largura, double Altura);
