using FluentAssertions;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Core.Tests.Vetor;

public class RefinamentoSubpixelTests
{
    private const int Largura = 6;
    private const int Altura = 4;

    private static ImagemRgba ConstruirImagem(Func<int, byte> corDaColuna)
    {
        var pixels = new byte[Largura * Altura * 4];
        for (var y = 0; y < Altura; y++)
        {
            for (var x = 0; x < Largura; x++)
            {
                var v = corDaColuna(x);
                var i = (y * Largura + x) * 4;
                pixels[i] = v;
                pixels[i + 1] = v;
                pixels[i + 2] = v;
                pixels[i + 3] = 255;
            }
        }
        return new ImagemRgba(Largura, Altura, pixels);
    }

    /// <summary>Refina o contorno da região "preta" (limiar &lt;=127) e devolve o X do vértice de topo da borda direita (a que separa preto de branco em x≈3).</summary>
    private static double XDaBordaDireitaRefinada(ImagemRgba imagem)
    {
        var indices = new int[Largura * Altura];
        for (var y = 0; y < Altura; y++)
        {
            for (var x = 0; x < Largura; x++)
            {
                var (r, _, _, _) = imagem.ObterPixel(x, y);
                indices[y * Largura + x] = r <= 127 ? 0 : 1;
            }
        }

        var contornos = ContornosDoMapa.Extrair(Largura, Altura, indices, indiceAlvo: 0);
        var refinado = RefinamentoSubpixel.Afinar(contornos[0].Pontos, imagem, new CorRgb(0, 0, 0));

        return refinado.Where(p => p.Y < 1).Max(p => p.X);
    }

    [Fact]
    public void Afinar_BordaNitidaSemMistura_NaoDeslocaAlemDoTrivial()
    {
        // Preto puro nas colunas 0-2, branco puro nas colunas 3-5 — a borda real já
        // coincide exatamente com a borda grosseira (x=3); não deve haver correção.
        var imagem = ConstruirImagem(x => x < 3 ? (byte)0 : (byte)255);

        XDaBordaDireitaRefinada(imagem).Should().BeApproximately(3.0, 1e-6);
    }

    [Fact]
    public void Afinar_BordaRealMaisParaADireita_DeslocaOVerticeParaADireita()
    {
        // Coluna 3 é uma mistura 30% preto / 70% branco — a borda real de verdade fica
        // em x≈3.3 (mais pra dentro do branco do que a fronteira grosseira em x=3).
        var imagem = ConstruirImagem(x => x < 3 ? (byte)0 : x == 3 ? (byte)Math.Round(255 * 0.7) : (byte)255);

        XDaBordaDireitaRefinada(imagem).Should().BeGreaterThan(3.0);
    }

    [Fact]
    public void Afinar_BordaRealMaisParaAEsquerda_DeslocaOVerticeParaAEsquerda()
    {
        // Coluna 2 é uma mistura 70% preto / 30% branco — a borda real fica em x≈2.7,
        // mais pra dentro do preto do que a fronteira grosseira em x=3.
        var imagem = ConstruirImagem(x => x < 2 ? (byte)0 : x == 2 ? (byte)Math.Round(255 * 0.3) : (byte)255);

        XDaBordaDireitaRefinada(imagem).Should().BeLessThan(3.0);
    }
}
