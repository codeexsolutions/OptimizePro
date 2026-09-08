namespace OptimizePro.Core.Encaixe.Busca;

/// <summary>
/// Porte de <c>receitasNaRoda</c> (§11.7): quando ativa, restringe a roda às receitas até
/// <see cref="Tolerancia"/> (padrão 6%) acima do melhor consumo já visto — mas nunca abaixo
/// de <see cref="Minimo"/> receitas, e uma receita nunca testada não é podada.
/// </summary>
public static class Poda
{
    public const double PodaToleranciaPadrao = 1.06;
    public const int PodaMinimoPadrao = 4;

    public static IReadOnlyList<PlacarDeReceita> ReceitasNaRoda(
        IReadOnlyList<PlacarDeReceita> placares, double? melhorGlobalConsumoCm, bool ativa,
        double tolerancia = PodaToleranciaPadrao, int minimo = PodaMinimoPadrao)
    {
        if (!ativa || melhorGlobalConsumoCm is null || placares.Count <= minimo)
            return placares;

        var limite = melhorGlobalConsumoCm.Value * tolerancia;
        var naRoda = placares.Where(p => p.MelhorConsumoCm is null || p.MelhorConsumoCm <= limite).ToList();

        if (naRoda.Count >= minimo)
            return naRoda;

        // nunca poda abaixo do mínimo — completa com as receitas mais bem colocadas restantes.
        return [.. placares.OrderBy(p => p.MelhorConsumoCm ?? double.MinValue).Take(minimo)];
    }
}
