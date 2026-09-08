using FluentAssertions;
using OptimizePro.Core;

namespace OptimizePro.Core.Tests;

public class GeometriaTests
{
    [Fact]
    public void AreaComSinal_QuadradoAntiHorario_RetornaAreaPositiva()
    {
        PontoXY[] quadrado =
        [
            new(0, 0),
            new(10, 0),
            new(10, 10),
            new(0, 10),
        ];

        Geometria.AreaComSinal(quadrado).Should().Be(100.0);
    }

    [Fact]
    public void AreaComSinal_MesmoContornoInvertido_RetornaAreaNegativa()
    {
        PontoXY[] horario =
        [
            new(0, 0),
            new(0, 10),
            new(10, 10),
            new(10, 0),
        ];

        Geometria.AreaComSinal(horario).Should().Be(-100.0);
    }

    [Fact]
    public void AreaComSinal_MenosDeTresPontos_RetornaZero()
    {
        Geometria.AreaComSinal([new PontoXY(0, 0), new PontoXY(1, 1)]).Should().Be(0.0);
    }

    [Fact]
    public void AreaComSinal_TrianguloRetangulo_CalculaMetadeDaBase()
    {
        PontoXY[] triangulo = [new(0, 0), new(4, 0), new(0, 3)];

        Geometria.AreaComSinal(triangulo).Should().Be(6.0);
    }

    [Fact]
    public void CaixaDeContorno_CalculaLimitesCorretos()
    {
        PontoXY[] pontos = [new(-2, 5), new(8, -3), new(1, 1)];

        var caixa = Geometria.CaixaDeContorno(pontos);

        caixa.MinX.Should().Be(-2);
        caixa.MinY.Should().Be(-3);
        caixa.MaxX.Should().Be(8);
        caixa.MaxY.Should().Be(5);
        caixa.Largura.Should().Be(10);
        caixa.Altura.Should().Be(8);
    }

    [Fact]
    public void LadoMenorDoContorno_Retangulo_RetornaMenorDimensao()
    {
        PontoXY[] retangulo = [new(0, 0), new(20, 0), new(20, 5), new(0, 5)];

        Geometria.LadoMenorDoContorno(retangulo).Should().Be(5);
    }

    [Fact]
    public void DistanciaEntre_PontosFormamTriangulo345()
    {
        var a = new PontoXY(0, 0);
        var b = new PontoXY(3, 4);

        Geometria.DistanciaEntre(a, b).Should().Be(5.0);
    }

    [Theory]
    [InlineData(5, 5, 0, 0, 10, 0, 5)]   // projeta no meio do segmento
    [InlineData(-3, 0, 0, 0, 10, 0, 3)]  // projeta antes do início (clamp em 'a')
    [InlineData(13, 0, 0, 0, 10, 0, 3)]  // projeta depois do fim (clamp em 'b')
    public void DistanciaAteSegmento_ProjecaoClampada(
        double px, double py, double ax, double ay, double bx, double by, double esperado)
    {
        var distancia = Geometria.DistanciaAteSegmento(
            new PontoXY(px, py), new PontoXY(ax, ay), new PontoXY(bx, by));

        distancia.Should().BeApproximately(esperado, 1e-9);
    }

    [Fact]
    public void DistanciaAteSegmento_SegmentoDegenerado_UsaDistanciaAoPonto()
    {
        var p = new PontoXY(3, 4);
        var a = new PontoXY(0, 0);

        Geometria.DistanciaAteSegmento(p, a, a).Should().Be(5.0);
    }

    [Fact]
    public void Simplificar_LinhaReta_RemovePontosColineares()
    {
        PontoXY[] pontos = [new(0, 0), new(1, 0.0000001), new(2, 0), new(3, 0), new(4, 0)];

        var resultado = Geometria.Simplificar(pontos, 0.01);

        resultado.Should().BeEquivalentTo([new PontoXY(0, 0), new PontoXY(4, 0)], o => o.WithStrictOrdering());
    }

    [Fact]
    public void Simplificar_PreservaExtremosSempre()
    {
        PontoXY[] pontos = [new(0, 0), new(1, 10), new(2, 0)];

        var resultado = Geometria.Simplificar(pontos, 100);

        resultado.First().Should().Be(pontos[0]);
        resultado.Last().Should().Be(pontos[^1]);
    }

    [Fact]
    public void Simplificar_PicoAcimaDaToleranciaEhMantido()
    {
        PontoXY[] pontos = [new(0, 0), new(5, 10), new(10, 0)];

        var resultado = Geometria.Simplificar(pontos, 1.0);

        resultado.Should().HaveCount(3);
        resultado[1].Should().Be(new PontoXY(5, 10));
    }

    [Fact]
    public void Simplificar_MenosDeTresPontos_RetornaCopiaSemAlterar()
    {
        PontoXY[] pontos = [new(0, 0), new(1, 1)];

        var resultado = Geometria.Simplificar(pontos, 5.0);

        resultado.Should().BeEquivalentTo(pontos, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Simplificar_QuadradoComPontosExtrasNasBordas_MantemOsQuatroCantos()
    {
        // Quadrado 10x10 com pontos intermediários exatamente sobre cada borda.
        PontoXY[] pontos =
        [
            new(0, 0), new(5, 0), new(10, 0),
            new(10, 5), new(10, 10),
            new(5, 10), new(0, 10),
            new(0, 5), new(0, 0),
        ];

        var resultado = Geometria.Simplificar(pontos, 0.001);

        resultado.Should().BeEquivalentTo(
            [new PontoXY(0, 0), new PontoXY(10, 0), new PontoXY(10, 10), new PontoXY(0, 10), new PontoXY(0, 0)],
            o => o.WithStrictOrdering());
    }
}
