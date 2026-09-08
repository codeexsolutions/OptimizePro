using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OptimizePro.Core;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Services.Encaixe;

public sealed record PecaDeImagemLida(string Nome, IReadOnlyList<PontoXY> Contorno, double LarguraCm, double AlturaCm, string Origem);

/// <summary>
/// Porte de <c>montarPecaDaImagem</c>/"tirar o fundo" (`encaixe.js`, §11.12/§4.4 do guia de
/// arquivo direto) — silhueta de uma arte PNG/JPG contra fundo claro, sem exigir vetor. Mais
/// simples que o pipeline completo de <c>OptimizePro.Services.Vetor</c> (que quantiza em N
/// cores pra desenho artístico): aqui só interessa UM contorno — o externo de maior área —
/// então basta uma máscara binária (fundo × arte) em vez de paleta.
/// </summary>
public static class LeitorDeImagemDeEncaixe
{
    private static readonly string[] Extensoes = ["png", "jpg", "jpeg"];

    /// <summary>Resolução assumida quando o arquivo não traz DPI gravado — mesmo padrão do molde vetorial/arte (§9.1, "impressão").</summary>
    public const double DpiPadrao = 300;

    /// <summary>Lado maior máximo antes de traçar o contorno — precisão de cm não exige percorrer os megapixels originais; a escala é ajustada pelo fator de redução, não descartada.</summary>
    public const int LadoMaiorMaximoPx = 1600;

    /// <summary>Canal RGB acima disto (e alfa abaixo de 128) conta como fundo — arte de impressão tende a fundo branco/quase-branco, com alguma perda de JPEG nas bordas.</summary>
    private const byte LimiarDeBranco = 235;

    public static bool SuportaExtensao(string extensao) => Extensoes.Contains(extensao.Trim('.').ToLowerInvariant());

    public static PecaDeImagemLida Ler(byte[] bytes, string nomeDoArquivo)
    {
        using var stream = new MemoryStream(bytes);
        using var original = new Bitmap(stream);

        var dpiDoArquivo = original.HorizontalResolution > 1 ? original.HorizontalResolution : (float?)null;
        var ppcmOriginal = (dpiDoArquivo ?? DpiPadrao) / 2.54;

        var ladoMaior = Math.Max(original.Width, original.Height);
        var fatorReducao = ladoMaior > LadoMaiorMaximoPx ? LadoMaiorMaximoPx / (double)ladoMaior : 1.0;
        var largura = Math.Max(1, (int)Math.Round(original.Width * fatorReducao));
        var altura = Math.Max(1, (int)Math.Round(original.Height * fatorReducao));
        var ppcm = ppcmOriginal * fatorReducao;

        var indices = MontarMascara(original, largura, altura);

        var contornos = ContornosDoMapa.Extrair(largura, altura, indices, 1);
        var externo = contornos.Where(c => !c.Furo).OrderByDescending(c => Math.Abs(Geometria.AreaComSinal(c.Pontos))).FirstOrDefault();

        if (externo is null)
            throw new InvalidOperationException($"\"{nomeDoArquivo}\": não encontrei nenhuma silhueta (o fundo precisa ser branco ou quase-branco).");

        var simplificado = Geometria.Simplificar(externo.Pontos, 1.0);
        var contornoCm = simplificado.Select(p => new PontoXY(p.X / ppcm, p.Y / ppcm)).ToList();

        var caixa = Geometria.CaixaDeContorno(contornoCm);
        var dpi = Math.Round(ppcm * 2.54);
        var nome = Path.GetFileNameWithoutExtension(nomeDoArquivo);

        return new PecaDeImagemLida(
            nome, contornoCm, Math.Round(caixa.Largura, 1), Math.Round(caixa.Altura, 1),
            $"{dpi} dpi{(dpiDoArquivo is null ? " (suposto)" : "")}");
    }

    private static int[] MontarMascara(Bitmap original, int largura, int altura)
    {
        using var redimensionada = new Bitmap(largura, altura, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(redimensionada))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(original, 0, 0, largura, altura);
        }

        var indices = new int[largura * altura];
        var dados = redimensionada.LockBits(new Rectangle(0, 0, largura, altura), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var linha = new byte[largura * 4];
            for (var y = 0; y < altura; y++)
            {
                Marshal.Copy(dados.Scan0 + y * dados.Stride, linha, 0, linha.Length);
                for (var x = 0; x < largura; x++)
                {
                    var i = x * 4; // B,G,R,A (Format32bppArgb)
                    var b = linha[i];
                    var g2 = linha[i + 1];
                    var r = linha[i + 2];
                    var a = linha[i + 3];

                    var ehFundo = a < 128 || (r >= LimiarDeBranco && g2 >= LimiarDeBranco && b >= LimiarDeBranco);
                    indices[y * largura + x] = ehFundo ? 0 : 1;
                }
            }
        }
        finally
        {
            redimensionada.UnlockBits(dados);
        }

        return indices;
    }
}
