namespace OptimizePro.Core.Vetor;

/// <summary>Amostragem de cor por interpolação bilinear — usada pelo refinamento subpixel (§14.4).</summary>
public static class AmostragemBilinear
{
    public static (double R, double G, double B) Amostrar(ImagemRgba imagem, double x, double y)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var x1 = x0 + 1;
        var y1 = y0 + 1;

        var fx = x - x0;
        var fy = y - y0;

        var (r00, g00, b00) = ObterCorClampada(imagem, x0, y0);
        var (r10, g10, b10) = ObterCorClampada(imagem, x1, y0);
        var (r01, g01, b01) = ObterCorClampada(imagem, x0, y1);
        var (r11, g11, b11) = ObterCorClampada(imagem, x1, y1);

        double Interp(double v00, double v10, double v01, double v11)
        {
            var topo = v00 * (1 - fx) + v10 * fx;
            var baixo = v01 * (1 - fx) + v11 * fx;
            return topo * (1 - fy) + baixo * fy;
        }

        return (Interp(r00, r10, r01, r11), Interp(g00, g10, g01, g11), Interp(b00, b10, b01, b11));
    }

    private static (double R, double G, double B) ObterCorClampada(ImagemRgba imagem, int x, int y)
    {
        var cx = Math.Clamp(x, 0, imagem.Largura - 1);
        var cy = Math.Clamp(y, 0, imagem.Altura - 1);
        var (r, g, b, _) = imagem.ObterPixel(cx, cy);
        return (r, g, b);
    }
}
