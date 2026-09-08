using FluentAssertions;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Core.Tests.Encaixe;

public class AgrupamentoCruzadoTests
{
    private static Mascara Quadrado2x2()
    {
        var silhueta = new bool[2, 2];
        silhueta[0, 0] = silhueta[0, 1] = silhueta[1, 0] = silhueta[1, 1] = true;
        return Mascara.DeSilhueta(silhueta, raioDeFolga: 0);
    }

    /// <summary>Mesma forma "escada" de AgrupamentoDeBlocosTests/EncostoDeFormasTests — interliga perfeitamente com a própria versão girada 180°.</summary>
    private static Mascara FormaEscada()
    {
        var silhueta = new bool[3, 2];
        silhueta[0, 0] = true;
        silhueta[0, 1] = true;
        silhueta[1, 0] = true;
        return Mascara.DeSilhueta(silhueta, raioDeFolga: 0);
    }

    private static Dictionary<int, Mascara> RotacoesDe(Mascara m0) =>
        new() { [0] = m0, [180] = RotacaoDeMascara.Rotacionar(m0, 180) };

    [Fact]
    public void Tentar_DuasFormasIguaisQueInterligam_AchaMesmaAreaQueADupla()
    {
        // A "cruzada" entre a MESMA forma e ela mesma tem que achar pelo menos um arranjo tão
        // bom quanto o que a dupla (AgrupamentoDeBlocos) já acha pra essa forma — área 6,
        // confirmado em AgrupamentoDeBlocosTests.FormasDoBloco_FormaComInterlockPerfeito.
        var mascaras = RotacoesDe(FormaEscada());

        var arranjo = AgrupamentoCruzado.Tentar(mascaras, mascaras);

        arranjo.Should().NotBeNull();
        arranjo!.MelhorArea.Should().Be(6);
        arranjo.Formas.Should().OnlyContain(f => f.Partes.Count == 2);
    }

    [Fact]
    public void Tentar_FormasQueNaoCompensam_DevolveNulo()
    {
        var quadrado = new Dictionary<int, Mascara> { [0] = Quadrado2x2() };

        var arranjo = AgrupamentoCruzado.Tentar(quadrado, quadrado);

        arranjo.Should().BeNull();
    }

    [Fact]
    public void Tentar_PartesSempreNaOrdemCanonicaAB()
    {
        // Mesmo quando a forma B "vence" como base internamente, Partes[0]/[1] devem
        // continuar correspondendo a A/B, não à ordem interna de qual venceu.
        var mascarasA = RotacoesDe(FormaEscada());
        var mascarasB = RotacoesDe(FormaEscada());

        var arranjo = AgrupamentoCruzado.Tentar(mascarasA, mascarasB);

        arranjo.Should().NotBeNull();
        arranjo!.Formas.Should().OnlyContain(f => f.Partes.Count == 2);
    }

    [Fact]
    public void Economia_ComArranjoUtil_EhPositiva()
    {
        var mascaras = RotacoesDe(FormaEscada());

        var arranjo = AgrupamentoCruzado.Tentar(mascaras, mascaras);

        arranjo!.Economia.Should().BeGreaterThan(0);
    }
}
