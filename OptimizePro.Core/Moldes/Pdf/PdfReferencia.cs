namespace OptimizePro.Core.Moldes.Pdf;

/// <summary>Referência indireta PDF ("N G R"). Objetos PDF são identificados por número (geração quase sempre 0).</summary>
public readonly record struct PdfReferencia(int Numero, int Geracao);
