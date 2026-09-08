using FluentAssertions;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Core.Tests.Vetor;

public class LimpezaDeCiscoTests
{
    [Fact]
    public void Limpar_ManchaDeUmPixelCercadaPorUmaSoCor_VirtaACorVizinha()
    {
        int[] indices =
        [
            0, 0, 0,
            0, 1, 0,
            0, 0, 0,
        ];

        var resultado = LimpezaDeCisco.Limpar(indices, largura: 3, altura: 3, tamanhoMinimo: 2);

        resultado.Should().AllSatisfy(i => i.Should().Be(0));
    }

    [Fact]
    public void Limpar_ManchaMaiorOuIgualAoMinimo_PermaneceInalterada()
    {
        int[] indices =
        [
            0, 0, 0,
            0, 1, 1,
            0, 0, 0,
        ];

        var resultado = LimpezaDeCisco.Limpar(indices, largura: 3, altura: 3, tamanhoMinimo: 2);

        resultado[4].Should().Be(1);
        resultado[5].Should().Be(1);
    }

    [Fact]
    public void Limpar_ManchaComVizinhosDeDuasCores_EscolheAMaisVotada()
    {
        // Mancha de 1 pixel (índice 9) no meio de uma linha: 3 vizinhos são cor 0, só 1 é cor 7.
        int[] indices =
        [
            0, 0, 0,
            0, 9, 0,
            0, 7, 0,
        ];

        var resultado = LimpezaDeCisco.Limpar(indices, largura: 3, altura: 3, tamanhoMinimo: 2);

        resultado[4].Should().Be(0); // 3 votos pra cor 0 contra 1 pra cor 7
    }

    [Fact]
    public void Limpar_PixelTransparente_NuncaEhLimpoNemContaComoVizinhoDominante()
    {
        int[] indices =
        [
            -1, -1, -1,
            -1, 5, -1,
            -1, -1, -1,
        ];

        var resultado = LimpezaDeCisco.Limpar(indices, largura: 3, altura: 3, tamanhoMinimo: 2);

        // sem nenhum vizinho de cor real, a mancha isolada fica como está.
        resultado[4].Should().Be(5);
        resultado.Where((_, i) => i != 4).Should().AllSatisfy(v => v.Should().Be(-1));
    }

    [Fact]
    public void Limpar_TamanhoMinimoMenorOuIgualA1_NaoAlteraNada()
    {
        int[] indices = [0, 1, 2];

        LimpezaDeCisco.Limpar(indices, largura: 3, altura: 1, tamanhoMinimo: 1).Should().Equal(indices);
        LimpezaDeCisco.Limpar(indices, largura: 3, altura: 1, tamanhoMinimo: 0).Should().Equal(indices);
    }
}
