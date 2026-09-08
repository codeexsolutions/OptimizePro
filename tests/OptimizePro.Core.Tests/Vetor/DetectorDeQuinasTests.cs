using FluentAssertions;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Core.Tests.Vetor;

public class DetectorDeQuinasTests
{
    [Fact]
    public void AcharQuinas_PontoEmLinhaReta_NaoEhQuina()
    {
        // ângulo entre entrada/saída = 180° (vetores opostos) — nunca fica abaixo de nenhum limiar razoável.
        List<PontoXY> pontos = [new(0, 0), new(1, 0), new(2, 0), new(2, 1)];

        var quinas = DetectorDeQuinas.AcharQuinas(pontos, quinaGraus: 55);

        quinas[1].Should().BeFalse();
    }

    [Fact]
    public void AcharQuinas_CantoDeNoventaGraus_NaoEhQuinaComLimiarPadrao()
    {
        // ângulo entre entrada/saída = 90° (não < 55).
        List<PontoXY> pontos = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

        var quinas = DetectorDeQuinas.AcharQuinas(pontos, quinaGraus: 55);

        quinas[1].Should().BeFalse();
    }

    [Fact]
    public void AcharQuinas_CantoDeNoventaGraus_EhQuinaComLimiarMaisAlto()
    {
        List<PontoXY> pontos = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

        var quinas = DetectorDeQuinas.AcharQuinas(pontos, quinaGraus: 100);

        quinas[1].Should().BeTrue(); // 90 < 100
    }

    [Fact]
    public void AcharQuinas_PicoBemFechado_EhQuinaComLimiarPadrao()
    {
        // ângulo entre entrada/saída = 45° (bem fechado, contorno quase volta sobre si mesmo).
        List<PontoXY> pontos = [new(0, 0), new(1, 0), new(0.9, 0.1)];

        var quinas = DetectorDeQuinas.AcharQuinas(pontos, quinaGraus: 55);

        quinas[1].Should().BeTrue(); // 45 < 55
    }

    [Fact]
    public void AcharQuinas_ArestaDegenerada_TrataComoRetoENaoQuina()
    {
        List<PontoXY> pontos = [new(0, 0), new(1, 0), new(1, 0), new(2, 0)]; // ponto duplicado

        var quinas = DetectorDeQuinas.AcharQuinas(pontos, quinaGraus: 55);

        quinas[1].Should().BeFalse();
    }
}
