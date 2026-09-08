using FluentAssertions;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Core.Tests.Encaixe;

public class RotacaoDeMascaraTests
{
    /// <summary>Máscara 2 colunas x 3 linhas com um único pixel marcado em (coluna=0, linha=0).</summary>
    private static Mascara MascaraAssimetrica()
    {
        var silhueta = new bool[2, 3];
        silhueta[0, 0] = true;
        return Mascara.DeSilhueta(silhueta, raioDeFolga: 0);
    }

    [Fact]
    public void Rotacionar_ZeroGraus_DevolveAMesmaMascara()
    {
        var origem = MascaraAssimetrica();

        RotacaoDeMascara.Rotacionar(origem, 0).Should().BeSameAs(origem);
    }

    [Fact]
    public void Rotacionar_360Graus_EquivaleAZero()
    {
        var origem = MascaraAssimetrica();

        RotacaoDeMascara.Rotacionar(origem, 360).Should().BeSameAs(origem);
    }

    [Fact]
    public void Rotacionar_90Graus_TrocaDimensoesEMovePontoMarcado()
    {
        var origem = MascaraAssimetrica(); // 2x3, pixel em (0,0)

        var girada = RotacaoDeMascara.Rotacionar(origem, 90);

        girada.Colunas.Should().Be(3); // Linhas da origem
        girada.Linhas.Should().Be(2);  // Colunas da origem
        girada.ObterDesenho(2, 0).Should().Be(1);
        ContarCelulasCheias(girada).Should().Be(1);
    }

    [Fact]
    public void Rotacionar_180Graus_MantemDimensoesEMovePontoParaOOposto()
    {
        var origem = MascaraAssimetrica(); // 2x3, pixel em (0,0)

        var girada = RotacaoDeMascara.Rotacionar(origem, 180);

        girada.Colunas.Should().Be(2);
        girada.Linhas.Should().Be(3);
        girada.ObterDesenho(1, 2).Should().Be(1); // (Colunas-1-0, Linhas-1-0)
        ContarCelulasCheias(girada).Should().Be(1);
    }

    [Fact]
    public void Rotacionar90DuasVezes_EquivaleARotacionar180Direto()
    {
        var origem = MascaraAssimetrica();

        var duasVezes90 = RotacaoDeMascara.Rotacionar(RotacaoDeMascara.Rotacionar(origem, 90), 90);
        var direto180 = RotacaoDeMascara.Rotacionar(origem, 180);

        duasVezes90.Colunas.Should().Be(direto180.Colunas);
        duasVezes90.Linhas.Should().Be(direto180.Linhas);
        duasVezes90.Desenho.Should().Equal(direto180.Desenho);
    }

    [Fact]
    public void Rotacionar_GrauNaoMultiploDe90_Lanca()
    {
        var origem = MascaraAssimetrica();

        var acao = () => RotacaoDeMascara.Rotacionar(origem, 45);

        acao.Should().Throw<ArgumentException>();
    }

    private static int ContarCelulasCheias(Mascara m)
    {
        var n = 0;
        for (var c = 0; c < m.Colunas; c++)
            for (var l = 0; l < m.Linhas; l++)
                if (m.ObterDesenho(c, l) == 1) n++;
        return n;
    }
}
