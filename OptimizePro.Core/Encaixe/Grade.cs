namespace OptimizePro.Core.Encaixe;

/// <summary>
/// Porte de <c>grade(larguraTecido, espaco)</c> (§11.2) — calcula o passo de célula e o
/// raio de dilatação que traduzem a folga pedida (cm) em número de células da grade.
/// </summary>
public readonly record struct Grade(double PassoCm, int Raio, double FolgaRealCm)
{
    public static Grade Calcular(double larguraTecidoCm, double espacoCm)
    {
        var maisGrossa = Math.Clamp(larguraTecidoCm / 300.0, 0.2, 1.0);

        if (espacoCm == 0)
            return new Grade(maisGrossa, 0, 0);

        var maisFina = larguraTecidoCm / 1000.0;
        var metade = espacoCm / 2.0;

        var partes = (int)Math.Ceiling(metade / maisGrossa);
        var passo = metade / partes;
        var raio = partes;

        if (passo < maisFina)
        {
            passo = maisFina;
            raio = (int)Math.Ceiling(metade / passo);
        }

        return new Grade(passo, raio, raio * passo * 2);
    }
}
