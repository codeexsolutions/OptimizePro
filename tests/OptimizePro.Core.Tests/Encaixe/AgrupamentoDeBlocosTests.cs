using FluentAssertions;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Core.Tests.Encaixe;

public class AgrupamentoDeBlocosTests
{
    private static Mascara Quadrado2x2()
    {
        var silhueta = new bool[2, 2];
        silhueta[0, 0] = silhueta[0, 1] = silhueta[1, 0] = silhueta[1, 1] = true;
        return Mascara.DeSilhueta(silhueta, raioDeFolga: 0);
    }

    /// <summary>Mesma forma "escada" de EncostoDeFormasTests — interliga perfeitamente com a própria versão girada 180°.</summary>
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
    public void FormasDoBloco_TamanhoMenorQue2_Lanca()
    {
        var acao = () => AgrupamentoDeBlocos.FormasDoBloco(Quadrado2x2(), 1);

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FormasDoBloco_FormaSemGanhoDeInterligar_NaoRetornaArranjoUtil()
    {
        // Dois quadrados cheios não interligam (área do bloco = área solta * 2, exatamente
        // no limite, nunca abaixo de 98%) — nenhum arranjo deve passar no filtro de utilidade.
        var arranjos = AgrupamentoDeBlocos.FormasDoBloco(Quadrado2x2(), 2);

        arranjos.Should().BeEmpty();
    }

    [Fact]
    public void FormasDoBloco_FormaComInterlockPerfeito_RetornaArranjoUtilDeDupla()
    {
        var arranjos = AgrupamentoDeBlocos.FormasDoBloco(FormaEscada(), 2);

        arranjos.Should().NotBeEmpty();
        arranjos.Should().Contain(f => AreaDaForma(f) == 6);
        arranjos.Should().OnlyContain(f => f.Partes.Count == 2);
    }

    [Fact]
    public void FormasDoBloco_Trio_MontaBlocoDeTresPartes()
    {
        var arranjos = AgrupamentoDeBlocos.FormasDoBloco(FormaEscada(), 3);

        arranjos.Should().NotBeEmpty();
        arranjos.Should().OnlyContain(f => f.Partes.Count == 3);

        // 3 peças x 3 células cada = 9 células preenchidas; a área envolvente nunca pode
        // ser menor que isso, e o filtro de utilidade já garante que é bem próxima.
        arranjos.Should().OnlyContain(f => AreaDaForma(f) >= 9);
    }
}
