using FluentAssertions;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Core.Tests.Vetor;

public class AmostragemBilinearTests
{
    private static ImagemRgba ImagemDeDuasColunas(byte corEsquerda, byte corDireita)
    {
        // 2x1: coluna 0 = corEsquerda, coluna 1 = corDireita.
        byte[] pixels = [corEsquerda, corEsquerda, corEsquerda, 255, corDireita, corDireita, corDireita, 255];
        return new ImagemRgba(2, 1, pixels);
    }

    [Fact]
    public void Amostrar_EmCoordenadaInteira_DevolveOPixelExatoSemMisturar()
    {
        var imagem = ImagemDeDuasColunas(0, 255);

        AmostragemBilinear.Amostrar(imagem, 0, 0).Should().Be((0.0, 0.0, 0.0));
        AmostragemBilinear.Amostrar(imagem, 1, 0).Should().Be((255.0, 255.0, 255.0));
    }

    [Fact]
    public void Amostrar_NoMeioEntreDoisPixels_MisturaMeioAMeio()
    {
        var imagem = ImagemDeDuasColunas(0, 200);

        var (r, g, b) = AmostragemBilinear.Amostrar(imagem, 0.5, 0);

        r.Should().BeApproximately(100, 1e-6);
        g.Should().BeApproximately(100, 1e-6);
        b.Should().BeApproximately(100, 1e-6);
    }

    [Fact]
    public void Amostrar_ForaDosLimites_ClampaNaBorda()
    {
        var imagem = ImagemDeDuasColunas(10, 20);

        AmostragemBilinear.Amostrar(imagem, -5, -5).Should().Be((10.0, 10.0, 10.0));
        AmostragemBilinear.Amostrar(imagem, 50, 50).Should().Be((20.0, 20.0, 20.0));
    }
}
