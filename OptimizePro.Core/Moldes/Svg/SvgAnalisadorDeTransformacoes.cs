using System.Globalization;
using System.Text.RegularExpressions;

namespace OptimizePro.Core.Moldes.Svg;

/// <summary>Parser do atributo <c>transform</c> do SVG (translate/scale/rotate/skewX/skewY/matrix).</summary>
public static partial class SvgAnalisadorDeTransformacoes
{
    [GeneratedRegex(@"(\w+)\s*\(([^)]*)\)")]
    private static partial Regex RegexFuncao();

    public static Transformacao2D Analisar(string? transformAttr)
    {
        if (string.IsNullOrWhiteSpace(transformAttr))
            return Transformacao2D.Identidade;

        var resultado = Transformacao2D.Identidade;

        foreach (Match m in RegexFuncao().Matches(transformAttr))
        {
            var nome = m.Groups[1].Value.ToLowerInvariant();
            var numeros = m.Groups[2].Value
                .Split([',', ' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseDoubleInvariante)
                .ToArray();

            var t = nome switch
            {
                "translate" => Transformacao2D.DeEscalaRotacaoTranslacao(
                    1, 1, 0, numeros.ElementAtOrDefault(0), numeros.ElementAtOrDefault(1)),
                "scale" => Transformacao2D.DeMatriz(
                    numeros.ElementAtOrDefault(0), 0, 0,
                    numeros.Length > 1 ? numeros[1] : numeros.ElementAtOrDefault(0), 0, 0),
                "rotate" => ConstruirRotacao(numeros),
                "skewx" => Transformacao2D.DeMatriz(1, 0, Math.Tan(GrausParaRad(numeros.ElementAtOrDefault(0))), 1, 0, 0),
                "skewy" => Transformacao2D.DeMatriz(1, Math.Tan(GrausParaRad(numeros.ElementAtOrDefault(0))), 0, 1, 0, 0),
                "matrix" when numeros.Length >= 6 =>
                    Transformacao2D.DeMatriz(numeros[0], numeros[1], numeros[2], numeros[3], numeros[4], numeros[5]),
                _ => Transformacao2D.Identidade,
            };

            resultado = resultado.ComposicaoCom(t);
        }

        return resultado;
    }

    private static Transformacao2D ConstruirRotacao(double[] numeros)
    {
        var anguloRad = GrausParaRad(numeros.ElementAtOrDefault(0));

        if (numeros.Length >= 3)
        {
            var cx = numeros[1];
            var cy = numeros[2];
            var paraOrigem = Transformacao2D.DeEscalaRotacaoTranslacao(1, 1, 0, -cx, -cy);
            var rotacao = Transformacao2D.DeEscalaRotacaoTranslacao(1, 1, anguloRad, 0, 0);
            var deVolta = Transformacao2D.DeEscalaRotacaoTranslacao(1, 1, 0, cx, cy);
            return deVolta.ComposicaoCom(rotacao).ComposicaoCom(paraOrigem);
        }

        return Transformacao2D.DeEscalaRotacaoTranslacao(1, 1, anguloRad, 0, 0);
    }

    private static double GrausParaRad(double graus) => graus * Math.PI / 180.0;

    private static double ParseDoubleInvariante(string valor) =>
        double.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
}
