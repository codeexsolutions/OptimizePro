using FluentAssertions;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Core.Tests.Encaixe;

public class EncaixadorPorCaixaTests
{
    [Fact]
    public void EncontrarMelhorPosicao_BinVazio_EncostaNoCantoInferiorEsquerdo()
    {
        var encaixador = new EncaixadorPorCaixa(10);

        var escolha = encaixador.EncontrarMelhorPosicao(5, 5, HeuristicaDeCaixa.Bl);

        escolha.Should().NotBeNull();
        escolha!.Value.X.Should().Be(0);
        escolha.Value.Y.Should().Be(0);
    }

    [Fact]
    public void ConfirmarEEncontrar_SegundaPecaBl_EncostaAoLadoDaPrimeira()
    {
        var encaixador = new EncaixadorPorCaixa(10);
        var primeira = encaixador.EncontrarMelhorPosicao(5, 5, HeuristicaDeCaixa.Bl)!.Value;
        encaixador.Confirmar(primeira);

        var segunda = encaixador.EncontrarMelhorPosicao(5, 5, HeuristicaDeCaixa.Bl);

        segunda.Should().NotBeNull();
        segunda!.Value.X.Should().Be(5);
        segunda.Value.Y.Should().Be(0);
    }

    [Fact]
    public void EncontrarMelhorPosicao_PecaMaisLargaQueOTecido_RetornaNulo()
    {
        var encaixador = new EncaixadorPorCaixa(10);

        encaixador.EncontrarMelhorPosicao(11, 1, HeuristicaDeCaixa.Bl).Should().BeNull();
    }

    [Fact]
    public void EncontrarMelhorPosicao_Baf_EscolheORetanguloComMenorAreaResidual()
    {
        var encaixador = new EncaixadorPorCaixa(100, alturaInicialCm: 10);

        // Coloca uma peça de 10x10 no meio da faixa livre (0,0,100,10): sobra um
        // retângulo à esquerda (0,0,40,10 -> área 400) e outro à direita
        // (50,0,50,10 -> área 500). Um item 10x10 cabe nos dois; BAF deve escolher o da
        // esquerda (sobra 300) em vez do da direita (sobra 400).
        encaixador.Confirmar(new EscolhaDeCaixa(40, 0, 10, 10, 0, 0));

        var escolha = encaixador.EncontrarMelhorPosicao(10, 10, HeuristicaDeCaixa.Baf);

        escolha.Should().NotBeNull();
        escolha!.Value.X.Should().Be(0);
        escolha.Value.Y.Should().Be(0);
    }

    [Fact]
    public void Encaixar_DuasPecasQuadradasBl_FicamLadoALadoNaMesmaAltura()
    {
        ItemParaCaixa a = new("a", 5, 5, 25);
        ItemParaCaixa b = new("b", 5, 5, 25);

        var resultado = EncaixePorCaixaExecutor.Encaixar(10, [(a, false), (b, false)], HeuristicaDeCaixa.Bl);

        resultado.ItensNaoEncaixados.Should().BeEmpty();
        resultado.Posicoes.Should().HaveCount(2);
        resultado.Posicoes[0].Should().Be(new PosicaoDeItemCaixa("a", 0, 0, 5, 5, false));
        resultado.Posicoes[1].Should().Be(new PosicaoDeItemCaixa("b", 5, 0, 5, 5, false));
        resultado.FundoMaximoCm.Should().Be(5);
        resultado.AreaRealCm2.Should().Be(50);
    }

    [Fact]
    public void Encaixar_PecaNaoCabeDeJeitoNenhum_VaiParaNaoEncaixados()
    {
        ItemParaCaixa quadrado = new("q", 12, 12, 144);

        var resultado = EncaixePorCaixaExecutor.Encaixar(10, [(quadrado, true)], HeuristicaDeCaixa.Bl);

        resultado.ItensNaoEncaixados.Should().ContainSingle().Which.Should().Be("q");
        resultado.AreaRealCm2.Should().Be(0);
    }

    [Fact]
    public void Encaixar_PermiteDeitar_UsaOrientacaoQueEncaixaQuandoANormalNaoCabe()
    {
        // 8x3 não cabe num tecido de largura 5 (8>5); deitada (3x8) cabe.
        ItemParaCaixa item = new("comprida", 8, 3, 24);

        var resultado = EncaixePorCaixaExecutor.Encaixar(5, [(item, true)], HeuristicaDeCaixa.Bl);

        resultado.ItensNaoEncaixados.Should().BeEmpty();
        resultado.Posicoes.Should().ContainSingle();
        var posicao = resultado.Posicoes[0];
        posicao.Deitada.Should().BeTrue();
        posicao.Largura.Should().Be(3);
        posicao.Altura.Should().Be(8);
    }

    [Fact]
    public void Encaixar_NaoPermiteDeitar_NaoTentaAOrientacaoTrocada()
    {
        ItemParaCaixa item = new("comprida", 8, 3, 24);

        var resultado = EncaixePorCaixaExecutor.Encaixar(5, [(item, false)], HeuristicaDeCaixa.Bl);

        resultado.ItensNaoEncaixados.Should().ContainSingle().Which.Should().Be("comprida");
    }
}
