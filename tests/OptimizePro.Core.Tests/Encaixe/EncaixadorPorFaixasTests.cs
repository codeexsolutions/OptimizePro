using FluentAssertions;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Core.Tests.Encaixe;

public class EncaixadorPorFaixasTests
{
    private static Mascara QuadradoMascara2x2()
    {
        var silhueta = new bool[2, 2];
        silhueta[0, 0] = silhueta[0, 1] = silhueta[1, 0] = silhueta[1, 1] = true;
        return Mascara.DeSilhueta(silhueta, raioDeFolga: 0);
    }

    private static ItemEncaixe ItemQuadrado(string id) =>
        new(id, new Dictionary<int, Mascara> { [0] = QuadradoMascara2x2() });

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Encaixar_ColunaDeCorteForaDoIntervalo_Lanca(int colunaDeCorte)
    {
        var acao = () => EncaixadorPorFaixas.Encaixar(6, colunaDeCorte, [], [], HeuristicaDeContorno.Fundo);

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Encaixar_UmItemEmCadaFaixa_SegundaFaixaTemXDeslocadoPeloCorte()
    {
        var itemA = ItemQuadrado("a");
        var itemB = ItemQuadrado("b");

        var resultado = EncaixadorPorFaixas.Encaixar(6, colunaDeCorte: 3, [(itemA, 0)], [(itemB, 0)], HeuristicaDeContorno.Fundo);

        resultado.ItensNaoEncaixados.Should().BeEmpty();
        resultado.Posicoes.Should().HaveCount(2);
        resultado.Posicoes.Should().Contain(new PosicaoDeItem("a", 0, 0, 0));   // faixa 1: local X=0
        resultado.Posicoes.Should().Contain(new PosicaoDeItem("b", 0, 3, 0));   // faixa 2: local X=0 + corte(3)
        resultado.FundoMaximo.Should().Be(2);
    }

    [Fact]
    public void Encaixar_ItemMaisLargoQueAFaixa1_VaiParaNaoEncaixadosMasFaixa2FunctionaNormal()
    {
        // Faixa 1 (largura 1) é estreita demais pro quadrado 2x2; faixa 2 (largura 5) encaixa.
        var itemEstreita = ItemQuadrado("estreita");
        var itemFolgado = ItemQuadrado("folgado");

        var resultado = EncaixadorPorFaixas.Encaixar(6, colunaDeCorte: 1, [(itemEstreita, 0)], [(itemFolgado, 0)], HeuristicaDeContorno.Fundo);

        resultado.ItensNaoEncaixados.Should().ContainSingle().Which.Should().Be("estreita");
        resultado.Posicoes.Should().ContainSingle();
        resultado.Posicoes[0].Should().Be(new PosicaoDeItem("folgado", 0, 1, 0)); // X local 0 + corte(1)
    }
}
