namespace OptimizePro.Core.Vetor;

public readonly record struct CirculoAjustado(PontoXY Centro, double Raio);

/// <summary>Ajuste de círculo por mínimos quadrados de Kåsa (§14.5).</summary>
public static class AjusteDeCirculo
{
    public static CirculoAjustado? Ajustar(IReadOnlyList<PontoXY> pontos)
    {
        if (pontos.Count < 3)
            return null;

        var mx = pontos.Average(p => p.X);
        var my = pontos.Average(p => p.Y);

        double sxx = 0, sxy = 0, syy = 0, sxz = 0, syz = 0;

        foreach (var p in pontos)
        {
            var u = p.X - mx;
            var v = p.Y - my;
            var z = u * u + v * v;

            sxx += u * u;
            sxy += u * v;
            syy += v * v;
            sxz += u * z;
            syz += v * z;
        }

        var det = sxx * syy - sxy * sxy;
        if (Math.Abs(det) < 1e-9)
            return null; // pontos colineares/degenerados — sem ajuste de círculo possível

        var cx = mx + (sxz * syy - syz * sxy) / (2 * det);
        var cy = my + (syz * sxx - sxz * sxy) / (2 * det);
        var centro = new PontoXY(cx, cy);

        var raio = pontos.Average(p => Geometria.DistanciaEntre(p, centro));

        return new CirculoAjustado(centro, raio);
    }
}
