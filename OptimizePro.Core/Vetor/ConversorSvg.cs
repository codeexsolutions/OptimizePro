using System.Text;

namespace OptimizePro.Core.Vetor;

/// <summary>Porte de §14.1 passo 9 — monta o comando <c>d</c> de um `&lt;path&gt;` a partir de um <see cref="CaminhoMontado"/>, e o `&lt;svg&gt;` final a partir das camadas por cor.</summary>
public static class ConversorSvg
{
    public static string ParaComandoDePath(CaminhoMontado caminho)
    {
        if (caminho.Segmentos.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.Append(CultureInvariante($"M{caminho.Inicio.X},{caminho.Inicio.Y}"));

        foreach (var segmento in caminho.Segmentos)
        {
            switch (segmento)
            {
                case SegmentoReta r:
                    sb.Append(CultureInvariante($" L{r.Fim.X},{r.Fim.Y}"));
                    break;

                case SegmentoArco a:
                    sb.Append(CultureInvariante(
                        $" A{a.Raio},{a.Raio} 0 {(a.GrandeArco ? 1 : 0)} {(a.Horario ? 1 : 0)} {a.Fim.X},{a.Fim.Y}"));
                    break;

                case SegmentoBezier c:
                    sb.Append(CultureInvariante($" C{c.Controle1.X},{c.Controle1.Y} {c.Controle2.X},{c.Controle2.Y} {c.Fim.X},{c.Fim.Y}"));
                    break;
            }
        }

        sb.Append(" Z");
        return sb.ToString();
    }

    /// <summary>Concatena um caminho por contorno (externo + furos) da mesma cor — usar <c>fill-rule="evenodd"</c> ao desenhar pra os furos vazarem.</summary>
    public static string ParaComandoDePathComFuros(IReadOnlyList<CaminhoMontado> caminhos) =>
        string.Join(" ", caminhos.Select(ParaComandoDePath).Where(d => d.Length > 0));

    public sealed record CamadaSvg(string CaminhoD, CorRgb Cor);

    public static string MontarSvg(double largura, double altura, IReadOnlyList<CamadaSvg> camadas)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInvariante($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{largura}\" height=\"{altura}\" viewBox=\"0 0 {largura} {altura}\">"));

        foreach (var camada in camadas)
        {
            if (camada.CaminhoD.Length == 0)
                continue;

            var corHex = $"#{camada.Cor.R:X2}{camada.Cor.G:X2}{camada.Cor.B:X2}";
            sb.Append(CultureInvariante($"<path d=\"{camada.CaminhoD}\" fill=\"{corHex}\" fill-rule=\"evenodd\"/>"));
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string CultureInvariante(FormattableString s) => FormattableString.Invariant(s);
}
