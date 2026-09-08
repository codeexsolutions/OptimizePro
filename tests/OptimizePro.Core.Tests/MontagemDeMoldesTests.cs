using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Moldes;

namespace OptimizePro.Core.Tests;

public class MontagemDeMoldesTests
{
    [Fact]
    public void MontarLacos_TracoUnicoJaFechado_PermaneceComoUmLaco()
    {
        Traco[] tracos =
        [
            new([new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0)], true),
        ];

        var lacos = MontagemDeMoldes.MontarLacos(tracos);

        lacos.Should().HaveCount(1);
        lacos[0].Fechado.Should().BeTrue();
    }

    [Fact]
    public void MontarLacos_QuatroTracosSoltosFormandoQuadrado_CosturaEmUmLacoFechado()
    {
        // Quadrado 10x10 desenhado como 4 segmentos soltos, em ordem embaralhada
        // e com um invertido, simulando traços de um DXF real.
        Traco[] tracos =
        [
            new([new(10, 0), new(10, 10)], false),       // lado direito
            new([new(0, 10), new(0, 0)], false),          // lado esquerdo (invertido)
            new([new(0, 0), new(10, 0)], false),          // base
            new([new(10, 10), new(0, 10)], false),        // topo
        ];

        var lacos = MontagemDeMoldes.MontarLacos(tracos);

        lacos.Should().HaveCount(1);
        lacos[0].Fechado.Should().BeTrue();
        Math.Abs(Geometria.AreaComSinal(lacos[0].Pontos)).Should().BeApproximately(100.0, 1e-6);
    }

    [Fact]
    public void MontarLacos_DoisContornosIndependentes_NaoSeMisturam()
    {
        Traco[] tracos =
        [
            new([new(0, 0), new(5, 0), new(5, 5), new(0, 5), new(0, 0)], true),
            new([new(100, 100), new(105, 100), new(105, 105), new(100, 105), new(100, 100)], true),
        ];

        var lacos = MontagemDeMoldes.MontarLacos(tracos);

        lacos.Should().HaveCount(2);
        lacos.Should().OnlyContain(l => l.Fechado);
    }

    [Fact]
    public void MontarLacos_TracoQueNaoFecha_PermaneceAbertoENaoTravaOAlgoritmo()
    {
        Traco[] tracos = [new([new(0, 0), new(10, 0), new(10, 5)], false)];

        var lacos = MontagemDeMoldes.MontarLacos(tracos);

        lacos.Should().HaveCount(1);
        lacos[0].Fechado.Should().BeFalse();
    }

    private static Traco QuadradoFechado(double x, double y, double lado) =>
        new([new(x, y), new(x + lado, y), new(x + lado, y + lado), new(x, y + lado), new(x, y)], true);

    [Fact]
    public void SepararPecasEFuros_QuadradoComFuroConcentrico_ClassificaCorretamente()
    {
        var externo = QuadradoFechado(0, 0, 10);
        var furo = QuadradoFechado(3, 3, 2);

        var lacos = new[]
        {
            new LacoCosturado(externo.Pontos, true),
            new LacoCosturado(furo.Pontos, true),
        };

        var pecas = MontagemDeMoldes.SepararPecasEFuros(lacos);

        pecas.Should().HaveCount(1);
        pecas[0].Furos.Should().HaveCount(1);
        Math.Abs(Geometria.AreaComSinal(pecas[0].Contorno)).Should().BeApproximately(100.0, 1e-6);
        Math.Abs(Geometria.AreaComSinal(pecas[0].Furos[0])).Should().BeApproximately(4.0, 1e-6);
    }

    [Fact]
    public void SepararPecasEFuros_DuasPecasSeparadasSemFuro_NenhumaViraFuroDaOutra()
    {
        var pecaA = QuadradoFechado(0, 0, 5);
        var pecaB = QuadradoFechado(100, 100, 5);

        var lacos = new[]
        {
            new LacoCosturado(pecaA.Pontos, true),
            new LacoCosturado(pecaB.Pontos, true),
        };

        var pecas = MontagemDeMoldes.SepararPecasEFuros(lacos);

        pecas.Should().HaveCount(2);
        pecas.Should().OnlyContain(p => p.Furos.Count == 0);
    }

    [Fact]
    public void SepararPecasEFuros_FolhaCobrindoTudoComPecaPequenaDentro_DescartaAFolha()
    {
        // A "folha" (moldura de página) cobre quase toda a bbox geral e contém uma
        // peça bem menor dentro — deve ser descartada, sobrando só a peça real.
        var folha = QuadradoFechado(0, 0, 100);

        // Precisa ser >=15% da área da folha (10000 * 0.15 = 1500 -> lado ~38.7cm).
        var pecaGrande = QuadradoFechado(10, 10, 40); // área 1600, acima do limiar

        var lacos = new[]
        {
            new LacoCosturado(folha.Pontos, true),
            new LacoCosturado(pecaGrande.Pontos, true),
        };

        var pecas = MontagemDeMoldes.SepararPecasEFuros(lacos);

        pecas.Should().HaveCount(1);
        Math.Abs(Geometria.AreaComSinal(pecas[0].Contorno)).Should().BeApproximately(1600.0, 1e-6);
    }

    [Fact]
    public void SepararPecasEFuros_LacoDuplicado_RemovidoRestandoUmaPeca()
    {
        var pecaOriginal = QuadradoFechado(0, 0, 10);
        var pecaDuplicada = QuadradoFechado(0, 0, 10);

        var lacos = new[]
        {
            new LacoCosturado(pecaOriginal.Pontos, true),
            new LacoCosturado(pecaDuplicada.Pontos, true),
        };

        var pecas = MontagemDeMoldes.SepararPecasEFuros(lacos);

        pecas.Should().HaveCount(1);
    }

    [Fact]
    public void SepararPecasEFuros_PecaDentroDeFuroDeOutraPeca_AninhaDoisNiveis()
    {
        // Peça grande com um furo, e dentro desse furo uma peça bem menor (aninhamento
        // de 2 níveis — cenário comum de moldes com recortes internos aproveitados).
        // O furo precisa ficar abaixo de 15% da área da peça grande (400*0.15=60);
        // senão colide com o critério de "é a folha" (peça grande cobre 100% da bbox
        // geral + contém laço >=15% de sua própria área) — ver comentário em
        // MontagemDeMoldes sobre essa ambiguidade quando a peça externa é um retângulo
        // perfeito com furo grande.
        var pecaGrande = QuadradoFechado(0, 0, 20);
        var furo = QuadradoFechado(5, 5, 6); // área 36 < 60
        var pecaPequenaDentroDoFuro = QuadradoFechado(7, 7, 2);

        var lacos = new[]
        {
            new LacoCosturado(pecaGrande.Pontos, true),
            new LacoCosturado(furo.Pontos, true),
            new LacoCosturado(pecaPequenaDentroDoFuro.Pontos, true),
        };

        var pecas = MontagemDeMoldes.SepararPecasEFuros(lacos);

        pecas.Should().HaveCount(2);
        var grande = pecas.Single(p => Math.Abs(Geometria.AreaComSinal(p.Contorno)) > 100);
        grande.Furos.Should().HaveCount(1);
        var pequena = pecas.Single(p => Math.Abs(Geometria.AreaComSinal(p.Contorno)) < 100);
        pequena.Furos.Should().BeEmpty();
    }

    [Fact]
    public void SepararPecasEFuros_SemLacosFechados_RetornaVazio()
    {
        var lacos = new[] { new LacoCosturado([new PontoXY(0, 0), new PontoXY(1, 1)], false) };

        MontagemDeMoldes.SepararPecasEFuros(lacos).Should().BeEmpty();
    }
}
