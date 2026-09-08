using System.Globalization;

namespace OptimizePro.Core.Moldes.Svg;

/// <summary>Conversão das formas básicas do SVG (rect/circle/ellipse/line/polyline/polygon) em pontos.</summary>
public static class SvgFormas
{
    public static IReadOnlyList<PontoXY> Retangulo(double x, double y, double largura, double altura, double rx, double ry)
    {
        if (largura <= 0 || altura <= 0)
            return [];

        if (rx <= 0 && ry <= 0)
            return [new(x, y), new(x + largura, y), new(x + largura, y + altura), new(x, y + altura), new(x, y)];

        if (rx <= 0) rx = ry;
        if (ry <= 0) ry = rx;
        rx = Math.Min(rx, largura / 2);
        ry = Math.Min(ry, altura / 2);

        var p1 = new PontoXY(x + rx, y);
        var p2 = new PontoXY(x + largura - rx, y);
        var p3 = new PontoXY(x + largura, y + ry);
        var p4 = new PontoXY(x + largura, y + altura - ry);
        var p5 = new PontoXY(x + largura - rx, y + altura);
        var p6 = new PontoXY(x + rx, y + altura);
        var p7 = new PontoXY(x, y + altura - ry);
        var p8 = new PontoXY(x, y + ry);

        List<PontoXY> pontos =
        [
            p1, p2,
            .. SvgCurvas.AmostrarArcoEliptico(p2, p3, rx, ry, 0, 0, 1),
            p4,
            .. SvgCurvas.AmostrarArcoEliptico(p4, p5, rx, ry, 0, 0, 1),
            p6,
            .. SvgCurvas.AmostrarArcoEliptico(p6, p7, rx, ry, 0, 0, 1),
            p8,
            .. SvgCurvas.AmostrarArcoEliptico(p8, p1, rx, ry, 0, 0, 1),
        ];

        return pontos;
    }

    public static IReadOnlyList<PontoXY> Circulo(double cx, double cy, double r) => Elipse(cx, cy, r, r);

    /// <summary>Elipse desenhada como dois semi-arcos (um único arco de 360° é degenerado em início==fim).</summary>
    public static IReadOnlyList<PontoXY> Elipse(double cx, double cy, double rx, double ry)
    {
        if (rx <= 0 || ry <= 0)
            return [];

        var leste = new PontoXY(cx + rx, cy);
        var oeste = new PontoXY(cx - rx, cy);

        List<PontoXY> pontos =
        [
            leste,
            .. SvgCurvas.AmostrarArcoEliptico(leste, oeste, rx, ry, 0, 0, 1),
            .. SvgCurvas.AmostrarArcoEliptico(oeste, leste, rx, ry, 0, 0, 1),
        ];

        return pontos;
    }

    public static IReadOnlyList<PontoXY> Linha(double x1, double y1, double x2, double y2) => [new(x1, y1), new(x2, y2)];

    public static IReadOnlyList<PontoXY>? PontosDeLista(string pontosAttr)
    {
        var tokens = pontosAttr.Split([',', ' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var numeros = new List<double>(tokens.Length);

        foreach (var tk in tokens)
        {
            if (!double.TryParse(tk, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return null;
            numeros.Add(v);
        }

        if (numeros.Count < 4 || numeros.Count % 2 != 0)
            return null;

        var pontos = new List<PontoXY>(numeros.Count / 2);
        for (var i = 0; i + 1 < numeros.Count; i += 2)
            pontos.Add(new PontoXY(numeros[i], numeros[i + 1]));

        return pontos;
    }
}
