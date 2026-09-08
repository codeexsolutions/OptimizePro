namespace OptimizePro.Core.Vetor;

/// <summary>
/// Pixels RGBA já decodificados (§11 da arquitetura: "este projeto só recebe
/// byte[]/Span&lt;byte&gt; RGBA já prontos" — decodificação/reamostragem fica na camada
/// de serviço, via ImageSharp).
/// </summary>
public sealed class ImagemRgba
{
    public int Largura { get; }
    public int Altura { get; }
    public IReadOnlyList<byte> Pixels { get; }

    public ImagemRgba(int largura, int altura, byte[] pixels)
    {
        if (pixels.Length != largura * altura * 4)
            throw new ArgumentException("pixels precisa ter largura*altura*4 bytes (RGBA).", nameof(pixels));

        Largura = largura;
        Altura = altura;
        Pixels = pixels;
    }

    public (byte R, byte G, byte B, byte A) ObterPixel(int x, int y)
    {
        var i = (y * Largura + x) * 4;
        return (Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
    }
}
