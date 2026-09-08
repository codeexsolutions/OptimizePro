using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Encaixe.Nfp;

namespace OptimizePro.Core.Tests.Encaixe.Nfp;

public class EncaixadorPorNfpTests
{
    private static List<PontoXY> Quadrado(double lado) => [new(0, 0), new(lado, 0), new(lado, lado), new(0, lado)];
    private static List<PontoXY> Retangulo(double largura, double altura) => [new(0, 0), new(largura, 0), new(largura, altura), new(0, altura)];

    /// <summary>Item sem giro (só rotação 0°) — a maioria destes testes não testa rotação, então não precisa das 4 variantes.</summary>
    private static ItemParaNfp ItemSemGiro(string id, IReadOnlyList<PontoXY> contorno) =>
        new(id, new Dictionary<int, IReadOnlyList<PontoXY>> { [0] = contorno });

    [Fact]
    public void Encaixar_UmUnicoItem_VaiParaOCantoInferiorEsquerdo()
    {
        var item = ItemSemGiro("a", Quadrado(2));

        var resultado = EncaixadorPorNfp.Encaixar(10, [item]);

        resultado.ItensNaoEncaixados.Should().BeEmpty();
        resultado.Posicoes.Should().ContainSingle();
        resultado.Posicoes[0].X.Should().BeApproximately(0, 1e-6);
        resultado.Posicoes[0].Y.Should().BeApproximately(0, 1e-6);
        resultado.FundoMaximo.Should().BeApproximately(2, 1e-6);
    }

    [Fact]
    public void Encaixar_DoisQuadradosIguais_FicamLadoALadoSemDesperdicioVertical()
    {
        var itemA = ItemSemGiro("a", Quadrado(2));
        var itemB = ItemSemGiro("b", Quadrado(2));

        var resultado = EncaixadorPorNfp.Encaixar(10, [itemA, itemB]);

        resultado.ItensNaoEncaixados.Should().BeEmpty();
        var posB = resultado.Posicoes.Single(p => p.ItemId == "b");
        posB.X.Should().BeApproximately(2, 1e-6); // encosta exatamente na borda direita de A
        posB.Y.Should().BeApproximately(0, 1e-6); // mesma altura — não empilha à toa
        resultado.FundoMaximo.Should().BeApproximately(2, 1e-6); // tecido consumido = altura de 1 peça só
    }

    [Fact]
    public void Encaixar_QuadradoERetangulo_RetanguloEncostaSemSubirAlemDoNecessario()
    {
        var quadrado = ItemSemGiro("q", Quadrado(2));
        var retangulo = ItemSemGiro("r", Retangulo(4, 1));

        var resultado = EncaixadorPorNfp.Encaixar(10, [quadrado, retangulo]);

        var posRetangulo = resultado.Posicoes.Single(p => p.ItemId == "r");
        posRetangulo.X.Should().BeApproximately(2, 1e-6);
        posRetangulo.Y.Should().BeApproximately(0, 1e-6);
        resultado.FundoMaximo.Should().BeApproximately(2, 1e-6); // limitado pela altura do quadrado, não do retângulo
    }

    [Fact]
    public void Encaixar_TerceiraPecaSemEspacoNaLinha_SobeParaAProximaFileiraEmVezDeFalhar()
    {
        // Tecido largura 5: só cabem 2 quadrados de lado 2 lado a lado (usa 4 de 5); o
        // terceiro não cabe ao lado (precisaria de 6), então sobe para cima do primeiro.
        var a = ItemSemGiro("a", Quadrado(2));
        var b = ItemSemGiro("b", Quadrado(2));
        var c = ItemSemGiro("c", Quadrado(2));

        var resultado = EncaixadorPorNfp.Encaixar(5, [a, b, c]);

        resultado.ItensNaoEncaixados.Should().BeEmpty();
        var posC = resultado.Posicoes.Single(p => p.ItemId == "c");
        posC.X.Should().BeApproximately(0, 1e-6);
        posC.Y.Should().BeApproximately(2, 1e-6);
        resultado.FundoMaximo.Should().BeApproximately(4, 1e-6);
    }

    [Fact]
    public void Encaixar_PecaMaisLargaQueOTecido_VaiParaNaoEncaixados()
    {
        var item = ItemSemGiro("largo", Retangulo(20, 1));

        var resultado = EncaixadorPorNfp.Encaixar(10, [item]);

        resultado.ItensNaoEncaixados.Should().ContainSingle().Which.Should().Be("largo");
        resultado.Posicoes.Should().BeEmpty();
    }

    [Fact]
    public void Encaixar_NenhumItem_RetornaResultadoVazioSemErro()
    {
        var resultado = EncaixadorPorNfp.Encaixar(10, []);

        resultado.Posicoes.Should().BeEmpty();
        resultado.ItensNaoEncaixados.Should().BeEmpty();
        resultado.FundoMaximo.Should().Be(0);
    }

    /// <summary>Retângulo 6×1 só cabe deitado (90°/270°, virando 1×6) num tecido de largura 5 — prova que o giro (§11.11) é testado de verdade, não só a rotação 0° do item.</summary>
    [Fact]
    public void Encaixar_PecaSoCabeGirada_EscolheARotacaoQueEncaixaEDevolveOGrauCerto()
    {
        var deitado = Retangulo(6, 1);
        var emPe = Retangulo(1, 6);
        var item = new ItemParaNfp("r", new Dictionary<int, IReadOnlyList<PontoXY>> { [0] = deitado, [90] = emPe });

        var resultado = EncaixadorPorNfp.Encaixar(5, [item]);

        resultado.ItensNaoEncaixados.Should().BeEmpty();
        var pos = resultado.Posicoes.Should().ContainSingle().Which;
        pos.RotacaoGraus.Should().Be(90);
        resultado.FundoMaximo.Should().BeApproximately(6, 1e-6);
    }

    /// <summary>Mesma peça 6×1, mas SEM a variante girada disponível — não cabe em nenhuma rotação oferecida, então vai pra não encaixadas em vez de forçar uma sobreposição.</summary>
    [Fact]
    public void Encaixar_PecaSoCabeGiradaMasRotacaoNaoDisponivel_VaiParaNaoEncaixados()
    {
        var item = ItemSemGiro("r", Retangulo(6, 1));

        var resultado = EncaixadorPorNfp.Encaixar(5, [item]);

        resultado.ItensNaoEncaixados.Should().ContainSingle().Which.Should().Be("r");
        resultado.Posicoes.Should().BeEmpty();
    }
}
