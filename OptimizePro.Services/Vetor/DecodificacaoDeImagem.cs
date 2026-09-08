using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Services.Vetor;

/// <summary>
/// Decodifica bytes crus (PNG/JPG/etc.) em <see cref="ImagemRgba"/> — o Core só recebe
/// pixels RGBA já prontos (ver comentário em <see cref="ImagemRgba"/>); decodificação e
/// reamostragem ficam aqui, via <c>System.Drawing.Common</c> (Windows-only — ok, o app é
/// desktop Windows; evita a licença comercial do ImageSharp acima de um teto de faturamento).
/// </summary>
internal static class DecodificacaoDeImagem
{
    /// <summary>Sempre reamostrada pro lado maior caber em 1800px antes de vetorizar (§14.1) — qualidade, não memória.</summary>
    public const int LadoMaiorMaximoPx = 1800;

    public static ImagemRgba Decodificar(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var original = new Bitmap(stream);

        var ladoMaior = Math.Max(original.Width, original.Height);
        var fator = ladoMaior > LadoMaiorMaximoPx ? LadoMaiorMaximoPx / (double)ladoMaior : 1.0;
        var largura = Math.Max(1, (int)Math.Round(original.Width * fator));
        var altura = Math.Max(1, (int)Math.Round(original.Height * fator));

        using var redimensionada = new Bitmap(largura, altura, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(redimensionada))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(original, 0, 0, largura, altura);
        }

        var pixels = new byte[largura * altura * 4];
        var dados = redimensionada.LockBits(new Rectangle(0, 0, largura, altura), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var linha = new byte[largura * 4];
            for (var y = 0; y < altura; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(dados.Scan0 + y * dados.Stride, linha, 0, linha.Length);
                for (var x = 0; x < largura; x++)
                {
                    // Format32bppArgb vem B,G,R,A por pixel — ImagemRgba espera R,G,B,A.
                    var origem = x * 4;
                    var destino = (y * largura + x) * 4;
                    pixels[destino] = linha[origem + 2];
                    pixels[destino + 1] = linha[origem + 1];
                    pixels[destino + 2] = linha[origem];
                    pixels[destino + 3] = linha[origem + 3];
                }
            }
        }
        finally
        {
            redimensionada.UnlockBits(dados);
        }

        return new ImagemRgba(largura, altura, pixels);
    }
}
