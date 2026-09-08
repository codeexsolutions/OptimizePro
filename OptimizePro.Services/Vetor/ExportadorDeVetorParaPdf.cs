using System.Text.RegularExpressions;
using OptimizePro.Core;
using OptimizePro.Core.Vetor;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace OptimizePro.Services.Vetor;

/// <summary>
/// "Salvar como PDF" (02/09/2026) — converte o SVG já gerado (por qualquer um dos dois
/// motores, próprio ou Potrace) em um PDF de verdade, com os caminhos como VETOR real (não
/// uma imagem embutida) — reaproveita <see cref="AnalisadorDePathSvg"/> pra reconstruir os
/// segmentos a partir do atributo <c>d</c> de cada camada.
/// </summary>
/// <remarks>
/// A imagem de origem não carrega informação de DPI/tamanho físico pretendido (o módulo
/// Vetor não tem campo de "largura desejada em cm") — assume-se 96 DPI ("pixel CSS", a
/// mesma convenção usada por navegadores e pela maioria das ferramentas raster→vetor
/// quando a imagem não informa a própria resolução), então 1px vira 72/96=0.75pt.
/// </remarks>
public static class ExportadorDeVetorParaPdf
{
    private const double PontosPorPixel = 72.0 / 96.0;

    private static readonly Regex RegexDeCamada = new("""<path d="([^"]+)" fill="(#[0-9A-Fa-f]{6})" fill-rule="evenodd"/>""", RegexOptions.Compiled);

    public static byte[] Converter(string svg, double larguraPx, double alturaPx)
    {
        using var documento = new PdfDocument();
        var pagina = documento.AddPage();
        pagina.Width = XUnit.FromPoint(larguraPx * PontosPorPixel);
        pagina.Height = XUnit.FromPoint(alturaPx * PontosPorPixel);

        using var graficos = XGraphics.FromPdfPage(pagina);
        graficos.ScaleTransform(PontosPorPixel);

        foreach (Match m in RegexDeCamada.Matches(svg))
        {
            var subcaminhos = AnalisadorDePathSvg.Analisar(m.Groups[1].Value);
            var cor = AnalisarCorHex(m.Groups[2].Value);

            var caminho = new XGraphicsPath { FillMode = XFillMode.Alternate };
            foreach (var subcaminho in subcaminhos)
                AdicionarSubcaminho(caminho, subcaminho);

            graficos.DrawPath(new XSolidBrush(cor), caminho);
        }

        using var ms = new MemoryStream();
        documento.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private static void AdicionarSubcaminho(XGraphicsPath caminho, CaminhoMontado subcaminho)
    {
        caminho.StartFigure();
        var atual = subcaminho.Inicio;

        foreach (var segmento in subcaminho.Segmentos)
        {
            switch (segmento)
            {
                case SegmentoReta r:
                    caminho.AddLine(atual.X, atual.Y, r.Fim.X, r.Fim.Y);
                    atual = r.Fim;
                    break;

                case SegmentoBezier c:
                    caminho.AddBezier(atual.X, atual.Y, c.Controle1.X, c.Controle1.Y, c.Controle2.X, c.Controle2.Y, c.Fim.X, c.Fim.Y);
                    atual = c.Fim;
                    break;

                case SegmentoArco a:
                    AdicionarArcoSvg(caminho, atual, a.Raio, a.GrandeArco, a.Horario, a.Fim);
                    atual = a.Fim;
                    break;
            }
        }

        caminho.CloseFigure();
    }

    /// <summary>
    /// Converte um arco no formato SVG (raio + flags + ponto final) pro formato que o
    /// PdfSharp entende (caixa envolvente + ângulo inicial + varredura) — fórmula padrão de
    /// "endpoint pra center parametrization" (SVG 1.1, Apêndice F.6), especializada pro caso
    /// de raio único (rx=ry, sem rotação de eixo) que é o único emitido pelos dois geradores
    /// deste projeto.
    /// </summary>
    private static void AdicionarArcoSvg(XGraphicsPath caminho, PontoXY inicio, double raio, bool grandeArco, bool horario, PontoXY fim)
    {
        var arco = CalcularArco(inicio, raio, grandeArco, horario, fim);
        if (arco is not { } a)
            return; // arco degenerado — nada a desenhar

        caminho.AddArc(a.CentroX - a.Raio, a.CentroY - a.Raio, 2 * a.Raio, 2 * a.Raio, a.AnguloInicialGraus, a.VarreduraGraus);
    }

    internal readonly record struct ArcoCalculado(double CentroX, double CentroY, double Raio, double AnguloInicialGraus, double VarreduraGraus);

    /// <summary>
    /// Converte um arco no formato SVG (raio + flags + ponto final) pro formato de
    /// "caixa envolvente + ângulo inicial + varredura" (usado tanto pelo PdfSharp quanto por
    /// GDI+/Avalonia) — fórmula padrão de "endpoint pra center parametrization" (SVG 1.1,
    /// Apêndice F.6.5), especializada pro caso de raio único (rx=ry, sem rotação de eixo) que
    /// é o único emitido pelos dois geradores deste projeto. Extraído numa função pura
    /// (sem tocar XGraphicsPath) pra dar pra testar a matemática isoladamente.
    /// </summary>
    internal static ArcoCalculado? CalcularArco(PontoXY inicio, double raio, bool grandeArco, bool horario, PontoXY fim)
    {
        if (raio <= 0 || (inicio.X == fim.X && inicio.Y == fim.Y))
            return null;

        // dx/dy aqui são exatamente o "(x1', y1')" da especificação (rotação phi=0).
        var dx = (inicio.X - fim.X) / 2.0;
        var dy = (inicio.Y - fim.Y) / 2.0;

        var r = raio;
        var distanciaAoQuadrado = dx * dx + dy * dy;
        var raioMinimo = Math.Sqrt(distanciaAoQuadrado); // ponto médio até cada extremo
        if (r < raioMinimo)
            r = raioMinimo; // SVG: se o raio pedido é pequeno demais pros pontos, escala pra caber

        var termo = r * r - distanciaAoQuadrado;
        var fator = Math.Sqrt(Math.Max(0, termo) / Math.Max(1e-12, distanciaAoQuadrado));
        var sinal = grandeArco == horario ? -1 : 1;
        var co = sinal * fator;

        // (cx', cy') = (co*dy, -co*dx) — com rx=ry=r o fator "rx/ry" da fórmula geral vira 1.
        var meioX = (inicio.X + fim.X) / 2.0;
        var meioY = (inicio.Y + fim.Y) / 2.0;
        var centroX = co * dy + meioX;
        var centroY = -co * dx + meioY;

        double AnguloGraus(double x, double y) => Math.Atan2(y - centroY, x - centroX) * 180.0 / Math.PI;

        var anguloInicial = AnguloGraus(inicio.X, inicio.Y);
        var anguloFinal = AnguloGraus(fim.X, fim.Y);

        var varredura = anguloFinal - anguloInicial;
        // Normaliza a varredura pro sentido (horário/anti-horário) e tamanho (grande/pequeno arco) certos.
        if (horario && varredura < 0) varredura += 360;
        if (!horario && varredura > 0) varredura -= 360;
        if (grandeArco && Math.Abs(varredura) < 180) varredura += varredura < 0 ? -360 : 360;
        if (!grandeArco && Math.Abs(varredura) > 180) varredura -= varredura < 0 ? -360 : 360;

        return new ArcoCalculado(centroX, centroY, r, anguloInicial, varredura);
    }

    private static XColor AnalisarCorHex(string hex)
    {
        var r = Convert.ToByte(hex.Substring(1, 2), 16);
        var g = Convert.ToByte(hex.Substring(3, 2), 16);
        var b = Convert.ToByte(hex.Substring(5, 2), 16);
        return XColor.FromArgb(r, g, b);
    }
}
