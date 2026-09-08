namespace OptimizePro.Core.Arte;

/// <summary>Posição e tamanho (cm) da arte já ajustada dentro da peça — origem no canto da peça, não no centro.</summary>
public readonly record struct RetanguloArte(double X, double Y, double Largura, double Altura);

/// <summary>Tamanho (cm) de um ladrilho de rapport, já com giro/escala aplicados.</summary>
public readonly record struct TamanhoRapport(double Largura, double Altura);
