namespace OptimizePro.Core.Encaixe;

/// <summary>
/// Porte de <c>formasDoBloco</c> (§11.8) — dupla junta a peça com a cópia invertida (180°,
/// "manga com manga invertida fecha quase um retângulo"); trio/quarteto estendem a ideia.
/// </summary>
public static class AgrupamentoDeBlocos
{
    /// <summary>Só arranjos com área envolvente pelo menos 2% menor que <paramref name="tamanho"/> peças soltas valem a pena.</summary>
    public const double FatorDeUtilidade = 0.98;

    /// <summary>
    /// Monta os blocos de <paramref name="tamanho"/> cópias de <paramref name="mascaraBase"/>
    /// (rotação 0), um partindo da 1ª cópia em 0° e outro em 180° — descarta os que não
    /// encaixam ou que não compensam frente a peças soltas.
    /// </summary>
    public static IReadOnlyList<Forma> FormasDoBloco(Mascara mascaraBase, int tamanho)
    {
        if (tamanho < 2)
            throw new ArgumentOutOfRangeException(nameof(tamanho), "Bloco só faz sentido para 2 ou mais cópias (dupla/trio/quarteto).");

        var formaSolta = Forma.DeMascaraUnica(mascaraBase);
        var areaSolta = (double)formaSolta.Colunas * (formaSolta.MaxBase + 1);
        var limiteUtil = areaSolta * tamanho * FatorDeUtilidade;

        var mascaraRot0 = RotacaoDeMascara.Rotacionar(mascaraBase, 0);
        var mascaraRot180 = RotacaoDeMascara.Rotacionar(mascaraBase, 180);

        List<Forma> arranjos = [];

        foreach (var (mascaraInicial, rotGrausInicial) in new[] { (mascaraRot0, 0), (mascaraRot180, 180) })
        {
            var bloco = Forma.DeMascaraUnica(mascaraInicial, rotGrausInicial);

            for (var k = 1; k < tamanho; k++)
            {
                var candidato0 = EncostoDeFormas.EncostarNaForma(bloco, mascaraRot0, 0);
                var candidato180 = EncostoDeFormas.EncostarNaForma(bloco, mascaraRot180, 180);

                bloco = AreaDaForma(candidato0) <= AreaDaForma(candidato180) ? candidato0 : candidato180;
            }

            arranjos.Add(bloco);
        }

        return [.. arranjos.Where(f => AreaDaForma(f) < limiteUtil)];
    }

    private static double AreaDaForma(Forma forma) => (double)forma.Colunas * (forma.MaxBase + 1);
}
