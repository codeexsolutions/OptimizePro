namespace OptimizePro.Core.Moldes.Svg;

/// <summary>
/// Flattening de curvas do path SVG: Bézier cúbica/quadrática (amostragem direta por
/// parâmetro t) e arco elíptico (parametrização por extremos — SVG 1.1 Apêndice F.6).
/// </summary>
public static class SvgCurvas
{
    private const int SegmentosPorBezier = 24;
    private const double PassoAngularAlvo = Math.PI / 36; // ~5°

    public static IReadOnlyList<PontoXY> AmostrarCubica(PontoXY p0, PontoXY p1, PontoXY p2, PontoXY p3)
    {
        var pontos = new List<PontoXY>(SegmentosPorBezier);
        for (var i = 1; i <= SegmentosPorBezier; i++)
        {
            var t = (double)i / SegmentosPorBezier;
            var mt = 1 - t;
            var x = mt * mt * mt * p0.X + 3 * mt * mt * t * p1.X + 3 * mt * t * t * p2.X + t * t * t * p3.X;
            var y = mt * mt * mt * p0.Y + 3 * mt * mt * t * p1.Y + 3 * mt * t * t * p2.Y + t * t * t * p3.Y;
            pontos.Add(new PontoXY(x, y));
        }
        pontos[^1] = p3;
        return pontos;
    }

    public static IReadOnlyList<PontoXY> AmostrarQuadratica(PontoXY p0, PontoXY p1, PontoXY p2)
    {
        var pontos = new List<PontoXY>(SegmentosPorBezier);
        for (var i = 1; i <= SegmentosPorBezier; i++)
        {
            var t = (double)i / SegmentosPorBezier;
            var mt = 1 - t;
            var x = mt * mt * p0.X + 2 * mt * t * p1.X + t * t * p2.X;
            var y = mt * mt * p0.Y + 2 * mt * t * p1.Y + t * t * p2.Y;
            pontos.Add(new PontoXY(x, y));
        }
        pontos[^1] = p2;
        return pontos;
    }

    /// <summary>Amostra o comando <c>A</c> do path (arco elíptico entre dois pontos dados).</summary>
    public static IReadOnlyList<PontoXY> AmostrarArcoEliptico(
        PontoXY inicio, PontoXY fim, double rx, double ry, double xRotGraus, int largeArcFlag, int sweepFlag)
    {
        if (rx == 0 || ry == 0 || (inicio.X == fim.X && inicio.Y == fim.Y))
            return [fim]; // degenerado — SVG trata como reta

        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        var phi = xRotGraus * Math.PI / 180.0;
        var cosPhi = Math.Cos(phi);
        var sinPhi = Math.Sin(phi);

        var dx2 = (inicio.X - fim.X) / 2.0;
        var dy2 = (inicio.Y - fim.Y) / 2.0;
        var x1l = cosPhi * dx2 + sinPhi * dy2;
        var y1l = -sinPhi * dx2 + cosPhi * dy2;

        var raioCheck = x1l * x1l / (rx * rx) + y1l * y1l / (ry * ry);
        if (raioCheck > 1)
        {
            var escala = Math.Sqrt(raioCheck);
            rx *= escala;
            ry *= escala;
        }

        var sinalSweep = largeArcFlag != sweepFlag ? 1.0 : -1.0;
        var num = rx * rx * ry * ry - rx * rx * y1l * y1l - ry * ry * x1l * x1l;
        var den = rx * rx * y1l * y1l + ry * ry * x1l * x1l;
        var coef = num <= 0 || den <= 0 ? 0 : sinalSweep * Math.Sqrt(Math.Max(0, num / den));
        var cxl = coef * (rx * y1l / ry);
        var cyl = coef * (-ry * x1l / rx);

        var cx = cosPhi * cxl - sinPhi * cyl + (inicio.X + fim.X) / 2.0;
        var cy = sinPhi * cxl + cosPhi * cyl + (inicio.Y + fim.Y) / 2.0;

        var anguloInicial = AnguloEntreVetores(1, 0, (x1l - cxl) / rx, (y1l - cyl) / ry);
        var deltaAngulo = AnguloEntreVetores((x1l - cxl) / rx, (y1l - cyl) / ry, (-x1l - cxl) / rx, (-y1l - cyl) / ry);

        if (sweepFlag == 0 && deltaAngulo > 0) deltaAngulo -= 2 * Math.PI;
        if (sweepFlag == 1 && deltaAngulo < 0) deltaAngulo += 2 * Math.PI;

        var numSegmentos = Math.Max(4, (int)Math.Ceiling(Math.Abs(deltaAngulo) / PassoAngularAlvo));
        var pontos = new List<PontoXY>(numSegmentos);

        for (var i = 1; i <= numSegmentos; i++)
        {
            var t = anguloInicial + deltaAngulo * i / numSegmentos;
            var x = cx + rx * Math.Cos(t) * cosPhi - ry * Math.Sin(t) * sinPhi;
            var y = cy + rx * Math.Cos(t) * sinPhi + ry * Math.Sin(t) * cosPhi;
            pontos.Add(new PontoXY(x, y));
        }

        pontos[^1] = fim;
        return pontos;
    }

    private static double AnguloEntreVetores(double ux, double uy, double vx, double vy)
    {
        var produto = ux * vx + uy * vy;
        var modulo = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
        var angulo = Math.Acos(Math.Clamp(produto / modulo, -1, 1));
        return ux * vy - uy * vx < 0 ? -angulo : angulo;
    }
}
