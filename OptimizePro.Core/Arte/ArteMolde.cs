namespace OptimizePro.Core.Arte;

/// <summary>
/// Porte de <c>public/arte-molde.js</c> (§4.3 da arquitetura, §9.1 da especificação) —
/// cálculo puro de posicionamento/tamanho da arte dentro do contorno da peça. Não desenha
/// nada: só devolve coordenadas/dimensões em cm; o desenho fica na camada de apresentação.
/// </summary>
public static class ArteMolde
{
    /// <summary>Teto de megapixels por peça usado para limitar o DPI de exportação.</summary>
    public const double TetoDeMegapixelsPorPeca = 26_000_000;

    public const double PpcmMinimo = 4.0;

    /// <summary>
    /// Calcula posição/tamanho (cm) da arte simples dentro do retângulo alvo (§9.1
    /// "encaixeDaArte"). <paramref name="arteW"/>/<paramref name="arteH"/> e
    /// <paramref name="alvoW"/>/<paramref name="alvoH"/> já em cm — a conversão
    /// px→cm (via ppcm do arquivo) é responsabilidade de quem chama.
    /// </summary>
    public static RetanguloArte EncaixeDaArte(double arteW, double arteH, double alvoW, double alvoH, AjusteArte ajuste)
    {
        var giro = NormalizarGiro(ajuste.GirauGraus);
        var deitada = giro is 90 or 270;
        var aW = deitada ? arteH : arteW;
        var aH = deitada ? arteW : arteH;

        double w, h;
        if (ajuste.Modo == ModoEncaixeArte.Esticar)
        {
            w = alvoW;
            h = alvoH;
        }
        else
        {
            var fator = ajuste.Modo == ModoEncaixeArte.Caber
                ? Math.Min(alvoW / aW, alvoH / aH)
                : Math.Max(alvoW / aW, alvoH / aH);
            w = aW * fator;
            h = aH * fator;
        }

        w *= ajuste.EscalaPercentual / 100.0;
        h *= ajuste.EscalaPercentual / 100.0;

        var x = (alvoW - w) / 2.0 + ajuste.DeslocamentoXCm;
        var y = (alvoH - h) / 2.0 + ajuste.DeslocamentoYCm;

        return new RetanguloArte(x, y, w, h);
    }

    /// <summary>
    /// Tamanho (cm) de um ladrilho de rapport em tamanho real, giro/escala aplicados
    /// (§9.1: "Rapport: ladrilho no tamanho real via ppcmArquivo").
    /// </summary>
    /// <param name="arteWpx">Largura da imagem original em pixels.</param>
    /// <param name="arteHpx">Altura da imagem original em pixels.</param>
    public static TamanhoRapport TamanhoDoRapport(double arteWpx, double arteHpx, AjusteArte ajuste)
    {
        if (ajuste.PpcmArquivo is not { } ppcm || ppcm <= 0)
        {
            throw new InvalidOperationException(
                "TamanhoDoRapport exige AjusteArte.PpcmArquivo definido (>0) — validar antes de " +
                "chamar (§9.1: o ppcm do arquivo original precisa ser persistido no ajuste, pois a " +
                "imagem salva não guarda mais o DPI).");
        }

        var giro = NormalizarGiro(ajuste.GirauGraus);
        var deitada = giro is 90 or 270;
        var wpx = deitada ? arteHpx : arteWpx;
        var hpx = deitada ? arteWpx : arteHpx;

        var escala = ajuste.EscalaPercentual / 100.0;
        return new TamanhoRapport(wpx / ppcm * escala, hpx / ppcm * escala);
    }

    /// <summary>
    /// DPI seguro de exportação (§9.1 "ppcmDaArte"): pede o DPI configurado, mas nunca
    /// deixa a peça passar de <see cref="TetoDeMegapixelsPorPeca"/> pixels.
    /// </summary>
    public static double PpcmDaArte(double larguraCm, double alturaCm, double dpi)
    {
        var ppcmPedido = Math.Max(PpcmMinimo, dpi / 2.54);

        var areaCm2 = larguraCm * alturaCm;
        var ppcmTeto = areaCm2 > 0 ? Math.Sqrt(TetoDeMegapixelsPorPeca / areaCm2) : ppcmPedido;

        return Math.Min(ppcmPedido, ppcmTeto);
    }

    private static int NormalizarGiro(int graus)
    {
        var g = graus % 360;
        return g < 0 ? g + 360 : g;
    }
}
