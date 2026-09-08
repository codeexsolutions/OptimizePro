namespace OptimizePro.Core.Moldes.Dxf;

/// <summary>Um par código/valor do formato DXF (ex.: código 10 = coordenada X).</summary>
public readonly record struct DxfPar(int Codigo, string Valor);
