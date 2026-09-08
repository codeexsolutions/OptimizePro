namespace OptimizePro.Core.Vetor;

/// <summary>
/// Porte de <c>posicaoDaCor</c> (§14.2) — espaço 3D usado para agrupar cores. Sem
/// "juntar sombras" é RGB puro; com ele, cromaticidade (proporção entre canais) com
/// luminância pesada decrescentemente, pra tratar luz/sombra da mesma cor como uma só.
/// </summary>
public static class EspacoDeAgrupamento
{
    public static (double P0, double P1, double P2) PosicaoDaCor(double r, double g, double b, double juntarSombras)
    {
        if (juntarSombras <= 0)
            return (r, g, b);

        var peso = 1 - 0.85 * Math.Min(1, juntarSombras / 100.0);
        var soma = r + g + b + 1;
        var p0 = r / soma * 441;
        var p1 = g / soma * 441;
        var p2 = (soma - 1) / 3.0 * peso;

        return (p0, p1, p2);
    }
}
