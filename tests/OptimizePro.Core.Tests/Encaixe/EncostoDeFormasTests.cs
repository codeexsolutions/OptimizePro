using FluentAssertions;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Core.Tests.Encaixe;

public class EncostoDeFormasTests
{
    private static Mascara Quadrado2x2()
    {
        var silhueta = new bool[2, 2];
        silhueta[0, 0] = silhueta[0, 1] = silhueta[1, 0] = silhueta[1, 1] = true;
        return Mascara.DeSilhueta(silhueta, raioDeFolga: 0);
    }

    /// <summary>
    /// Forma "escada": coluna 0 cheia (2 linhas), coluna 1 só a linha 0, coluna 2 vazia —
    /// 3 células preenchidas no total. Girada 180°, encaixa exatamente no vão da original
    /// (a coluna vazia de uma cobre a coluna cheia da outra), formando um bloco 3x2 = 6
    /// células sem desperdício algum — o mínimo teórico possível (2 peças x 3 células).
    /// </summary>
    private static Mascara FormaEscada()
    {
        var silhueta = new bool[3, 2];
        silhueta[0, 0] = true;
        silhueta[0, 1] = true;
        silhueta[1, 0] = true;
        return Mascara.DeSilhueta(silhueta, raioDeFolga: 0);
    }

    private static double AreaDaForma(Forma forma) => forma.Colunas * (forma.MaxBase + 1);

    [Fact]
    public void EncostarNaForma_DoisQuadradosIdenticos_MelhorArranjoEhLadoALadoSemGanho()
    {
        var bloco = Forma.DeMascaraUnica(Quadrado2x2());

        var combinado = EncostoDeFormas.EncostarNaForma(bloco, Quadrado2x2());

        // Não há como interligar dois quadrados cheios — a melhor área possível é a soma
        // das duas peças lado a lado, sem sobra nem ganho (2*2 x 2 = 8).
        AreaDaForma(combinado).Should().Be(8);
        combinado.Partes.Should().HaveCount(2);
    }

    [Fact]
    public void EncostarNaForma_FormaEscadaComSuaVersaoInvertida_EncaixaSemDesperdicio()
    {
        var bloco = Forma.DeMascaraUnica(FormaEscada());
        var invertida = RotacaoDeMascara.Rotacionar(FormaEscada(), 180);

        var combinado = EncostoDeFormas.EncostarNaForma(bloco, invertida);

        // 3 células preenchidas em cada peça = 6 no total; se a área envolvente bater
        // exatamente 6, é porque encaixou sem nenhum espaço vazio sobrando.
        AreaDaForma(combinado).Should().Be(6);
    }

    [Fact]
    public void EncostarNaForma_ResultadoPreservaAsDuasPartesComSeusDeslocamentos()
    {
        var bloco = Forma.DeMascaraUnica(Quadrado2x2());

        var combinado = EncostoDeFormas.EncostarNaForma(bloco, Quadrado2x2());

        combinado.Partes.Should().HaveCount(2);
        combinado.Partes[0].Mascara.Should().BeSameAs(bloco.Partes[0].Mascara);
    }
}
