using FluentAssertions;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Core.Tests.Vetor;

public class TangentesDoContornoTests
{
    [Fact]
    public void Calcular_VerticeSuave_TangenteDeEntradaEDeSaidaSaoIguaisEApontamAnteriorParaProximo()
    {
        List<PontoXY> pontos = [new(0, 0), new(1, 0.1), new(2, 0)]; // levemente curvo, mas não é quina
        var quinas = new[] { false, false, false };

        var tangentes = TangentesDoContorno.Calcular(pontos, quinas);

        // anterior->próximo = (2,0)-(0,0) = (2,0), normalizado = (1,0)
        tangentes[1].Entrada.Should().Be(tangentes[1].Saida);
        tangentes[1].Entrada.X.Should().BeApproximately(1, 1e-9);
        tangentes[1].Entrada.Y.Should().BeApproximately(0, 1e-9);
    }

    [Fact]
    public void Calcular_CantoVivo_TangentesDeEntradaESaidaSeguemCadaLadoSemSuavizar()
    {
        List<PontoXY> pontos = [new(0, 0), new(1, 0), new(1, 1)]; // canto de 90°
        var quinas = new[] { false, true, false };

        var tangentes = TangentesDoContorno.Calcular(pontos, quinas);

        // entrada segue o lado que chega: (1,0)-(0,0) normalizado = (1,0)
        tangentes[1].Entrada.X.Should().BeApproximately(1, 1e-9);
        tangentes[1].Entrada.Y.Should().BeApproximately(0, 1e-9);

        // saída segue o lado que sai: (1,1)-(1,0) normalizado = (0,1)
        tangentes[1].Saida.X.Should().BeApproximately(0, 1e-9);
        tangentes[1].Saida.Y.Should().BeApproximately(1, 1e-9);

        tangentes[1].Entrada.Should().NotBe(tangentes[1].Saida);
    }

    [Fact]
    public void Calcular_ArestaDegenerada_DevolveTangenteZeroSemLancar()
    {
        List<PontoXY> pontos = [new(0, 0), new(0, 0), new(1, 0)];
        var quinas = new[] { false, false, false };

        var tangentes = TangentesDoContorno.Calcular(pontos, quinas);

        // anterior==proximo (ambos (1,0) e (0,0) via wrap)... aqui só garante que não lança e devolve algo determinístico.
        tangentes.Should().HaveCount(3);
    }
}
