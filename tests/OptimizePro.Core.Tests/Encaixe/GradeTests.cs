using FluentAssertions;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Core.Tests.Encaixe;

public class GradeTests
{
    [Fact]
    public void Calcular_SemEspaco_PassoMaximoESemRaio()
    {
        var g = Grade.Calcular(300, 0);

        g.PassoCm.Should().BeApproximately(1.0, 1e-9); // clamp(300/300,0.2,1) = 1
        g.Raio.Should().Be(0);
        g.FolgaRealCm.Should().Be(0);
    }

    [Fact]
    public void Calcular_MaisGrossaRespeitaOClampMinimo()
    {
        var g = Grade.Calcular(30, 0); // 30/300=0.1 -> clamp para 0.2

        g.PassoCm.Should().BeApproximately(0.2, 1e-9);
    }

    [Fact]
    public void Calcular_ComEspaco_DerivaPassoERaioDaMetade()
    {
        var g = Grade.Calcular(150, 1); // maisGrossa=0.5, metade=0.5, partes=1

        g.PassoCm.Should().BeApproximately(0.5, 1e-9);
        g.Raio.Should().Be(1);
        g.FolgaRealCm.Should().BeApproximately(1.0, 1e-9); // raio*passo*2
    }

    [Fact]
    public void Calcular_EspacoMuitoPequeno_CaiParaOPassoMaisFino()
    {
        // maisGrossa=1 (1000/300 clampado), maisFina=1000/1000=1.0, metade=0.05
        // -> partes=ceil(0.05/1)=1, passo=0.05 < maisFina(1.0) -> corrige para passo=1.0
        var g = Grade.Calcular(1000, 0.1);

        g.PassoCm.Should().BeApproximately(1.0, 1e-9);
        g.Raio.Should().Be(1); // ceil(0.05/1.0)
        g.FolgaRealCm.Should().BeApproximately(2.0, 1e-9);
    }
}
