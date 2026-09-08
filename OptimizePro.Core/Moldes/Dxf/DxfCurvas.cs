namespace OptimizePro.Core.Moldes.Dxf;

/// <summary>
/// Amostragem das curvas do DXF em polilinhas de pontos — bulge de LWPOLYLINE/POLYLINE
/// (§8.1 "Bulge → arco"), ARC/CIRCLE, ELLIPSE paramétrica e avaliação De Boor de SPLINE
/// (B-spline não-racional; peso de NURBS — código 41 por ponto de controle — é ignorado,
/// suficiente para o caso comum de contorno de molde).
/// </summary>
public static class DxfCurvas
{
    private const double PassoAngularAlvo = Math.PI / 36; // ~5°

    /// <summary>
    /// Converte um segmento com bulge (curvatura) em pontos de arco entre p1 e p2,
    /// SEM incluir p1 (para poder ser concatenado direto após o ponto anterior).
    /// Fórmula padrão DXF: ângulo = 4*atan(bulge); sinal do bulge define o sentido.
    /// </summary>
    public static IReadOnlyList<PontoXY> PontosDoArcoPorBulge(PontoXY p1, PontoXY p2, double bulge)
    {
        if (Math.Abs(bulge) < 1e-12)
            return [p2];

        var corda = Geometria.DistanciaEntre(p1, p2);
        if (corda < 1e-12)
            return [p2];

        var anguloTotal = 4 * Math.Atan(bulge);
        var raio = corda / (2 * Math.Sin(anguloTotal / 2));
        var altura = raio * Math.Cos(anguloTotal / 2);

        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var perpX = -dy / corda;
        var perpY = dx / corda;

        var centro = new PontoXY((p1.X + p2.X) / 2 + perpX * altura, (p1.Y + p2.Y) / 2 + perpY * altura);
        var anguloInicial = Math.Atan2(p1.Y - centro.Y, p1.X - centro.X);
        var raioAbs = Math.Abs(raio);

        var numSegmentos = Math.Max(4, (int)Math.Ceiling(Math.Abs(anguloTotal) / PassoAngularAlvo));
        var pontos = new List<PontoXY>(numSegmentos);

        for (var i = 1; i <= numSegmentos; i++)
        {
            var angulo = anguloInicial + anguloTotal * i / numSegmentos;
            pontos.Add(new PontoXY(centro.X + raioAbs * Math.Cos(angulo), centro.Y + raioAbs * Math.Sin(angulo)));
        }

        pontos[^1] = p2; // evita erro de acumulação de ponto flutuante no fechamento
        return pontos;
    }

    /// <summary>Amostra um arco (ou círculo completo, com 0/360) sentido anti-horário.</summary>
    public static IReadOnlyList<PontoXY> PontosDoArco(PontoXY centro, double raio, double anguloInicialGraus, double anguloFinalGraus)
    {
        var inicioRad = anguloInicialGraus * Math.PI / 180.0;
        var fimRad = anguloFinalGraus * Math.PI / 180.0;
        if (fimRad <= inicioRad)
            fimRad += 2 * Math.PI;

        var sweep = fimRad - inicioRad;
        var numSegmentos = Math.Max(8, (int)Math.Ceiling(sweep / PassoAngularAlvo));

        var pontos = new List<PontoXY>(numSegmentos + 1);
        for (var i = 0; i <= numSegmentos; i++)
        {
            var angulo = inicioRad + sweep * i / numSegmentos;
            pontos.Add(new PontoXY(centro.X + raio * Math.Cos(angulo), centro.Y + raio * Math.Sin(angulo)));
        }

        return pontos;
    }

    /// <summary>Amostra uma elipse (ou arco elíptico) a partir do ponto extremo do eixo maior e da razão eixo menor/maior.</summary>
    public static IReadOnlyList<PontoXY> PontosDaElipse(
        double centroX, double centroY, double eixoMaiorX, double eixoMaiorY, double razao,
        double paramInicial, double paramFinal)
    {
        var comprimentoMaior = Math.Sqrt(eixoMaiorX * eixoMaiorX + eixoMaiorY * eixoMaiorY);
        var comprimentoMenor = comprimentoMaior * razao;
        var rotacao = Math.Atan2(eixoMaiorY, eixoMaiorX);

        var fim = paramFinal;
        if (fim <= paramInicial)
            fim += 2 * Math.PI;

        var sweep = fim - paramInicial;
        var numSegmentos = Math.Max(8, (int)Math.Ceiling(sweep / PassoAngularAlvo));

        var cosRot = Math.Cos(rotacao);
        var sinRot = Math.Sin(rotacao);

        var pontos = new List<PontoXY>(numSegmentos + 1);
        for (var i = 0; i <= numSegmentos; i++)
        {
            var t = paramInicial + sweep * i / numSegmentos;
            var x0 = comprimentoMaior * Math.Cos(t);
            var y0 = comprimentoMenor * Math.Sin(t);
            pontos.Add(new PontoXY(centroX + x0 * cosRot - y0 * sinRot, centroY + x0 * sinRot + y0 * cosRot));
        }

        return pontos;
    }

    /// <summary>Avalia um ponto de uma B-spline não-racional em <paramref name="t"/> pelo algoritmo de De Boor.</summary>
    public static PontoXY AvaliarBSpline(int grau, IReadOnlyList<double> nos, IReadOnlyList<PontoXY> controle, double t)
    {
        var n = controle.Count - 1;
        var k = EncontrarIntervalo(nos, grau, n, t);

        var trabalho = new PontoXY[grau + 1];
        for (var i = 0; i <= grau; i++)
            trabalho[i] = controle[k - grau + i];

        for (var r = 1; r <= grau; r++)
        {
            for (var i = grau; i >= r; i--)
            {
                var indiceNo = k - grau + i;
                var denominador = nos[indiceNo + grau - r + 1] - nos[indiceNo];
                var alpha = denominador > 1e-12 ? (t - nos[indiceNo]) / denominador : 0.0;

                trabalho[i] = new PontoXY(
                    (1 - alpha) * trabalho[i - 1].X + alpha * trabalho[i].X,
                    (1 - alpha) * trabalho[i - 1].Y + alpha * trabalho[i].Y);
            }
        }

        return trabalho[grau];
    }

    private static int EncontrarIntervalo(IReadOnlyList<double> nos, int grau, int n, double t)
    {
        if (t >= nos[n + 1])
            return n;
        if (t <= nos[grau])
            return grau;

        var baixo = grau;
        var alto = n + 1;
        var meio = (baixo + alto) / 2;

        while (t < nos[meio] || t >= nos[meio + 1])
        {
            if (t < nos[meio])
                alto = meio;
            else
                baixo = meio;
            meio = (baixo + alto) / 2;
        }

        return meio;
    }
}
