using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Core.Tests.Vetor;

public class ContornosDoMapaTests
{
    [Fact]
    public void Extrair_RetanguloDeUmaCor_ProduzUmContornoComAreaCorreta()
    {
        // 4x3, colunas 1-2 são a cor 5 (2 de largura x 3 de altura = área 6), resto é cor 0.
        int largura = 4, altura = 3;
        var indices = new int[largura * altura];
        for (var l = 0; l < altura; l++)
        {
            indices[l * largura + 1] = 5;
            indices[l * largura + 2] = 5;
        }

        var contornos = ContornosDoMapa.Extrair(largura, altura, indices, indiceAlvo: 5);

        contornos.Should().HaveCount(1);
        contornos[0].Furo.Should().BeFalse();
        Math.Abs(Geometria.AreaComSinal(contornos[0].Pontos)).Should().BeApproximately(6.0, 1e-6);
    }

    [Fact]
    public void Extrair_CorComBuracoNoMeio_ProduzContornoExternoEFuro()
    {
        int largura = 5, altura = 5;
        var indices = new int[largura * altura];
        for (var i = 0; i < indices.Length; i++) indices[i] = 3;
        indices[2 * largura + 2] = 0; // buraco de 1 pixel no centro

        var contornos = ContornosDoMapa.Extrair(largura, altura, indices, indiceAlvo: 3);

        contornos.Should().HaveCount(2);

        var externo = contornos.Should().ContainSingle(c => !c.Furo).Subject;
        Math.Abs(Geometria.AreaComSinal(externo.Pontos)).Should().BeApproximately(25.0, 1e-6);

        var furo = contornos.Should().ContainSingle(c => c.Furo).Subject;
        Math.Abs(Geometria.AreaComSinal(furo.Pontos)).Should().BeApproximately(1.0, 1e-6);
    }

    [Fact]
    public void Extrair_IndiceQueNaoAparece_RetornaListaVazia()
    {
        int largura = 3, altura = 3;
        var indices = new int[largura * altura]; // tudo 0

        ContornosDoMapa.Extrair(largura, altura, indices, indiceAlvo: 9).Should().BeEmpty();
    }
}
