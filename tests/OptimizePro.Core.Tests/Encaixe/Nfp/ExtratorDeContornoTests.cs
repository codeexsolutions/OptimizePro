using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Encaixe;
using OptimizePro.Core.Encaixe.Nfp;

namespace OptimizePro.Core.Tests.Encaixe.Nfp;

public class ExtratorDeContornoTests
{
    private static Mascara MascaraDeGrade(bool[,] grade) => Mascara.DeSilhueta(grade, raioDeFolga: 0);

    [Fact]
    public void ExtrairContornos_UmaCelulaUnica_ProduzUmQuadradoUnitario()
    {
        var grade = new bool[1, 1];
        grade[0, 0] = true;

        var contornos = ExtratorDeContorno.ExtrairContornos(MascaraDeGrade(grade));

        contornos.Should().HaveCount(1);
        contornos[0].Furo.Should().BeFalse();
        Math.Abs(Geometria.AreaComSinal(contornos[0].Pontos)).Should().BeApproximately(1.0, 1e-6);
    }

    [Fact]
    public void ExtrairContornos_RetanguloCheio2x3_AreaBateComOTamanho()
    {
        var grade = new bool[2, 3];
        for (var c = 0; c < 2; c++)
            for (var l = 0; l < 3; l++)
                grade[c, l] = true;

        var contornos = ExtratorDeContorno.ExtrairContornos(MascaraDeGrade(grade));

        contornos.Should().HaveCount(1);
        contornos[0].Furo.Should().BeFalse();
        Math.Abs(Geometria.AreaComSinal(contornos[0].Pontos)).Should().BeApproximately(6.0, 1e-6);
    }

    [Fact]
    public void ExtrairContornos_RetanguloComFuroNoMeio_ProduzContornoExternoEFuroSeparados()
    {
        var grade = new bool[5, 5];
        for (var c = 0; c < 5; c++)
            for (var l = 0; l < 5; l++)
                grade[c, l] = true;
        grade[2, 2] = false; // um furo de 1 célula no centro

        var contornos = ExtratorDeContorno.ExtrairContornos(MascaraDeGrade(grade));

        contornos.Should().HaveCount(2);

        var externo = contornos.Should().ContainSingle(c => !c.Furo).Subject;
        Math.Abs(Geometria.AreaComSinal(externo.Pontos)).Should().BeApproximately(25.0, 1e-6);

        var furo = contornos.Should().ContainSingle(c => c.Furo).Subject;
        Math.Abs(Geometria.AreaComSinal(furo.Pontos)).Should().BeApproximately(1.0, 1e-6);
    }

    [Fact]
    public void ExtrairContornos_MascaraVazia_NaoProduzContornoNenhum()
    {
        var grade = new bool[3, 3];

        ExtratorDeContorno.ExtrairContornos(MascaraDeGrade(grade)).Should().BeEmpty();
    }

    [Fact]
    public void ExtrairContornos_FormaEmL_ProduzUmUnicoContornoComAreaCorreta()
    {
        // L: coluna 0 cheia (3 linhas), colunas 1-2 só a linha 2 -> área = 3 + 1 + 1 = 5.
        var grade = new bool[3, 3];
        grade[0, 0] = grade[0, 1] = grade[0, 2] = true;
        grade[1, 2] = true;
        grade[2, 2] = true;

        var contornos = ExtratorDeContorno.ExtrairContornos(MascaraDeGrade(grade));

        contornos.Should().HaveCount(1);
        Math.Abs(Geometria.AreaComSinal(contornos[0].Pontos)).Should().BeApproximately(5.0, 1e-6);
    }
}
