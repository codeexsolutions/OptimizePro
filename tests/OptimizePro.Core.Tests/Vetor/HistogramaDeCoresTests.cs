using FluentAssertions;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Core.Tests.Vetor;

public class HistogramaDeCoresTests
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

    [Fact]
    public void Construir_ImagemDeUmaCorSolida_ProduzUmaUnicaEntrada()
    {
        var imagem = ConstruirImagem(10, 10, (_, _) => ((byte)50, (byte)60, (byte)70, (byte)255));

        var histograma = HistogramaDeCores.Construir(imagem);

        histograma.Should().ContainSingle();
        histograma[0].Peso.Should().Be(100);
    }

    /// <summary>
    /// Regressão (02/09/2026) — achado com logo real de degradê saindo com cor vazando entre
    /// regiões: um anel de "mistura" (cor de anti-aliasing entre um bloco e o fundo, que não
    /// representa nem uma região nem a outra de verdade) não deveria contar como cor própria
    /// no histograma — só pixels de MIOLO (longe de transição) contam, desde que o miolo seja
    /// pelo menos 5% do total (aqui é, de sobra: o fundo sozinho já é a maioria da imagem).
    /// </summary>
    [Fact]
    public void Construir_AnelDeMisturaEntreDuasRegioes_NaoAparecoComoCorPropria()
    {
        var imagem = ConstruirImagem(20, 20, (x, y) =>
        {
            var dentroDoAnel = x is >= 7 and <= 12 && y is >= 7 and <= 12;
            var dentroDoBloco = x is >= 8 and <= 11 && y is >= 8 and <= 11;

            if (dentroDoBloco) return ((byte)200, (byte)200, (byte)200, (byte)255);
            if (dentroDoAnel) return ((byte)100, (byte)100, (byte)100, (byte)255);
            return ((byte)0, (byte)0, (byte)0, (byte)255);
        });

        var histograma = HistogramaDeCores.Construir(imagem);

        histograma.Should().NotContain(e => Math.Abs(e.R - 100) < 5 && Math.Abs(e.G - 100) < 5 && Math.Abs(e.B - 100) < 5,
            "o anel é só mistura de anti-aliasing entre o fundo e o bloco, não uma cor real de nenhuma das duas regiões");
    }

    /// <summary>Se o miolo for pequeno demais (imagem fininha, quase só borda), cai pra contar tudo em vez de descartar informação demais.</summary>
    [Fact]
    public void Construir_ImagemQuaseTodaDeBorda_CaiParaContarTodosOsPixels()
    {
        // Um "L" de 1px de espessura, com folga de fundo transparente em volta (inclusive no
        // canto) — todo pixel tem vizinho 4-conectado transparente, então miolo real é zero.
        var imagem = ConstruirImagem(12, 12, (x, y) =>
            (x == 1 && y is >= 1 and <= 10) || (y == 10 && x is >= 1 and <= 10)
                ? ((byte)255, (byte)0, (byte)0, (byte)255)
                : ((byte)0, (byte)0, (byte)0, (byte)0));

        var acao = () => HistogramaDeCores.Construir(imagem);

        acao.Should().NotThrow();
        var histograma = acao();
        histograma.Sum(e => e.Peso).Should().Be(19); // 10 (coluna) + 10 (linha) - 1 (canto compartilhado)
    }
}
