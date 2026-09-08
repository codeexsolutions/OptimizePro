namespace OptimizePro.Core.Moldes.Plt;

/// <summary>Amostragem de arcos HP-GL (AA/AR/CI) — sweep com sinal explícito (positivo = anti-horário).</summary>
public static class PltCurvas
{
    private const double PassoAngularAlvo = Math.PI / 36; // ~5°

    /// <summary>Pontos de <paramref name="anguloInicialRad"/> a <paramref name="anguloInicialRad"/>+sweep, incluindo os dois extremos.</summary>
    public static IReadOnlyList<PontoXY> PontosDoArcoPorSweep(PontoXY centro, double raio, double anguloInicialRad, double sweepRad)
    {
        var raioAbs = Math.Abs(raio);
        var numSegmentos = Math.Max(4, (int)Math.Ceiling(Math.Abs(sweepRad) / PassoAngularAlvo));

        var pontos = new List<PontoXY>(numSegmentos + 1);
        for (var i = 0; i <= numSegmentos; i++)
        {
            var angulo = anguloInicialRad + sweepRad * i / numSegmentos;
            pontos.Add(new PontoXY(centro.X + raioAbs * Math.Cos(angulo), centro.Y + raioAbs * Math.Sin(angulo)));
        }

        return pontos;
    }
}
