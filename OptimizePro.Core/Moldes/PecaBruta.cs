namespace OptimizePro.Core.Moldes;

/// <summary>
/// Resultado da classificação peça/furo (§8.1 "separarPecasEFuros"), ainda em unidade
/// do arquivo original — conversão para cm, nomeação e filtro de tamanho mínimo
/// acontecem depois, no leitor de formato específico (que conhece a unidade do arquivo).
/// </summary>
public sealed record PecaBruta(IReadOnlyList<PontoXY> Contorno, IReadOnlyList<IReadOnlyList<PontoXY>> Furos);
