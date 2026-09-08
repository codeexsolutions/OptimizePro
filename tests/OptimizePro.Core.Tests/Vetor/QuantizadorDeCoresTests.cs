using FluentAssertions;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Core.Tests.Vetor;

public class QuantizadorDeCoresTests
{
    private static ImagemRgba ConstruirImagem(int largura, int altura, Func<int, int, (byte R, byte G, byte B, byte A)> gerador)
    {
        var pixels = new byte[largura * altura * 4];

        for (var y = 0; y < altura; y++)
        {
            for (var x = 0; x < largura; x++)
            {
                var (r, g, b, a) = gerador(x, y);
                var i = (y * largura + x) * 4;
                pixels[i] = r;
                pixels[i + 1] = g;
                pixels[i + 2] = b;
                pixels[i + 3] = a;
            }
        }

        return new ImagemRgba(largura, altura, pixels);
    }

    private static int IndiceMaisPertoDe(IReadOnlyList<CorRgb> paleta, byte r, byte g, byte b)
    {
        var melhorIndice = 0;
        var melhorDistancia = double.MaxValue;

        for (var i = 0; i < paleta.Count; i++)
        {
            var d = Math.Pow(paleta[i].R - r, 2) + Math.Pow(paleta[i].G - g, 2) + Math.Pow(paleta[i].B - b, 2);
            if (d < melhorDistancia)
            {
                melhorDistancia = d;
                melhorIndice = i;
            }
        }

        return melhorIndice;
    }

    [Fact]
    public void Quantizar_ImagemDeUmaCorSolida_ProduzUmaUnicaCorNaPaleta()
    {
        var imagem = ConstruirImagem(3, 3, (_, _) => ((byte)100, (byte)150, (byte)200, (byte)255));

        var resultado = QuantizadorDeCores.Quantizar(imagem, new OpcoesDeQuantizacao(3));

        resultado.Paleta.Should().HaveCount(1); // só 1 cor distinta — não dá pra dividir mais
        resultado.Paleta[0].R.Should().Be(100);
        resultado.Paleta[0].G.Should().Be(150);
        resultado.Paleta[0].B.Should().Be(200);
        resultado.IndicesPorPixel.Should().AllSatisfy(i => i.Should().Be(0));
    }

    [Fact]
    public void Quantizar_ImagemComDuasCoresBemDistintas_SeparaCadaMetadeCorretamente()
    {
        var imagem = ConstruirImagem(4, 4, (x, _) => x < 2
            ? ((byte)255, (byte)0, (byte)0, (byte)255)
            : ((byte)0, (byte)0, (byte)255, (byte)255));

        var resultado = QuantizadorDeCores.Quantizar(imagem, new OpcoesDeQuantizacao(2));

        resultado.Paleta.Should().HaveCount(2);

        var indiceVermelho = IndiceMaisPertoDe(resultado.Paleta, 255, 0, 0);
        var indiceAzul = IndiceMaisPertoDe(resultado.Paleta, 0, 0, 255);
        indiceVermelho.Should().NotBe(indiceAzul);

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var esperado = x < 2 ? indiceVermelho : indiceAzul;
                resultado.IndicesPorPixel[y * 4 + x].Should().Be(esperado);
            }
        }
    }

    [Fact]
    public void Quantizar_ImagemTotalmenteTransparente_RetornaPaletaVaziaEIndicesMenosUm()
    {
        var imagem = ConstruirImagem(2, 2, (_, _) => ((byte)0, (byte)0, (byte)0, (byte)0));

        var resultado = QuantizadorDeCores.Quantizar(imagem, new OpcoesDeQuantizacao(4));

        resultado.Paleta.Should().BeEmpty();
        resultado.IndicesPorPixel.Should().AllSatisfy(i => i.Should().Be(-1));
    }

    [Fact]
    public void Quantizar_PixelTransparenteMisturadoComOpaco_FicaComIndiceMenosUm()
    {
        var imagem = ConstruirImagem(2, 1, (x, _) => x == 0
            ? ((byte)255, (byte)0, (byte)0, (byte)255)
            : ((byte)0, (byte)0, (byte)0, (byte)0));

        var resultado = QuantizadorDeCores.Quantizar(imagem, new OpcoesDeQuantizacao(2));

        resultado.Paleta.Should().HaveCount(1);
        resultado.IndicesPorPixel[0].Should().Be(0);
        resultado.IndicesPorPixel[1].Should().Be(-1);
    }

    [Fact]
    public void Quantizar_NumeroDeCoresPedidoMaiorQueCoresDistintas_ParaNoMaximoPossivel()
    {
        var imagem = ConstruirImagem(2, 1, (x, _) => x == 0
            ? ((byte)255, (byte)0, (byte)0, (byte)255)
            : ((byte)0, (byte)0, (byte)255, (byte)255));

        var resultado = QuantizadorDeCores.Quantizar(imagem, new OpcoesDeQuantizacao(10));

        resultado.Paleta.Should().HaveCount(2); // só existem 2 cores possíveis, mesmo pedindo 10
    }

    [Fact]
    public void Quantizar_NumeroDeCoresMenorQue1_Lanca()
    {
        var imagem = ConstruirImagem(1, 1, (_, _) => ((byte)1, (byte)1, (byte)1, (byte)255));

        var acao = () => QuantizadorDeCores.Quantizar(imagem, new OpcoesDeQuantizacao(0));

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SugerirNumeroDeCores_ImagemDeUmaCorSolida_Sugere2ComoMinimo()
    {
        var imagem = ConstruirImagem(20, 20, (_, _) => ((byte)10, (byte)10, (byte)10, (byte)255));

        QuantizadorDeCores.SugerirNumeroDeCores(imagem).Should().Be(2);
    }

    [Fact]
    public void SugerirNumeroDeCores_QuatroBlocosDeCoresBemDiferentes_SugereQuatro()
    {
        // 4 blocos grandes, cores bem afastadas (mais que a distância mínima de agrupamento).
        var imagem = ConstruirImagem(40, 40, (x, y) =>
        {
            var bloco = (x < 20 ? 0 : 1) + (y < 20 ? 0 : 2);
            return bloco switch
            {
                0 => ((byte)0, (byte)0, (byte)0, (byte)255),       // preto
                1 => ((byte)255, (byte)255, (byte)255, (byte)255), // branco
                2 => ((byte)220, (byte)30, (byte)30, (byte)255),   // vermelho
                _ => ((byte)30, (byte)30, (byte)220, (byte)255),   // azul
            };
        });

        QuantizadorDeCores.SugerirNumeroDeCores(imagem).Should().Be(4);
    }

    [Fact]
    public void SugerirNumeroDeCores_ManchaMinusculaDeRuidoDeBorda_NaoContaComoCorPropria()
    {
        // Um bloco grande de uma cor + um ÚNICO pixel de outra cor bem diferente — o pixel
        // isolado é peso desprezível (< 0.4% do total) e não deve inflar a sugestão.
        var imagem = ConstruirImagem(30, 30, (x, y) => x == 0 && y == 0
            ? ((byte)255, (byte)0, (byte)255, (byte)255)
            : ((byte)10, (byte)10, (byte)10, (byte)255));

        QuantizadorDeCores.SugerirNumeroDeCores(imagem).Should().Be(2); // clamp no mínimo, não 2 cores "de verdade" contadas
    }

    [Fact]
    public void SugerirNumeroDeCores_NuncaPassaDoMaximoNemFicaAbaixoDoMinimo()
    {
        var imagem = ConstruirImagem(1, 1, (_, _) => ((byte)5, (byte)5, (byte)5, (byte)255));

        var sugestao = QuantizadorDeCores.SugerirNumeroDeCores(imagem, minimo: 3, maximo: 8);

        sugestao.Should().BeInRange(3, 8);
    }
}
