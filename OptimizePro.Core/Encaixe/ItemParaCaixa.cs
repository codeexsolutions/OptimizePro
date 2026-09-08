namespace OptimizePro.Core.Encaixe;

/// <summary>
/// Uma peça para o encaixador por caixa — só a caixa delimitadora importa para posicionar;
/// <see cref="AreaRealCm2"/> (da silhueta de verdade) é usada só para o % de aproveitamento.
/// </summary>
public sealed record ItemParaCaixa(string Id, double LarguraCm, double AlturaCm, double AreaRealCm2);
