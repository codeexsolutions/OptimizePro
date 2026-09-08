namespace OptimizePro.Core.Moldes;

/// <summary>Resultado da costura de um ou mais traços soltos em uma cadeia contínua.</summary>
public sealed record LacoCosturado(IReadOnlyList<PontoXY> Pontos, bool Fechado);
