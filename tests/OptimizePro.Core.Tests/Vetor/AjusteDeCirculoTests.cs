using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Core.Tests.Vetor;

public class AjusteDeCirculoTests
{
    private static List<PontoXY> PontosDoCirculo(double cx, double cy, double r, int n)
    {
        var pontos = new List<PontoXY>();
        for (var i = 0; i < n; i++)
        {
            var angulo = 2 * Math.PI * i / n;
            pontos.Add(new PontoXY(cx + r * Math.Cos(angulo), cy + r * Math.Sin(angulo)));
        }
        return pontos;
    }

    [Fact]
    public void Ajustar_PontosSobreUmCirculoPerfeito_AchaCentroERaioCorretos()
    {
        var pontos = PontosDoCirculo(5, 5, 3, 16);

        var resultado = AjusteDeCirculo.Ajustar(pontos);

        resultado.Should().NotBeNull();
        resultado!.Value.Centro.X.Should().BeApproximately(5, 1e-6);
        resultado.Value.Centro.Y.Should().BeApproximately(5, 1e-6);
        resultado.Value.Raio.Should().BeApproximately(3, 1e-6);
    }

    [Fact]
    public void Ajustar_MenosDeTresPontos_RetornaNulo()
    {
        AjusteDeCirculo.Ajustar([new(0, 0), new(1, 1)]).Should().BeNull();
    }

    [Fact]
    public void Ajustar_PontosColineares_RetornaNuloPorSeremDegenerados()
    {
        List<PontoXY> pontos = [new(0, 0), new(1, 0), new(2, 0), new(3, 0)];

        AjusteDeCirculo.Ajustar(pontos).Should().BeNull();
    }

    [Fact]
    public void Ajustar_MeioCirculoAssimetrico_AindaAchaOCentroCorreto()
    {
        // Só 5 pontos num arco de 90°, não a volta inteira — o ajuste de mínimos
        // quadrados ainda deve achar o círculo certo por trás.
        List<PontoXY> pontos = [];
        for (var i = 0; i <= 4; i++)
        {
            var angulo = i * (Math.PI / 2) / 4;
            pontos.Add(new PontoXY(10 * Math.Cos(angulo), 10 * Math.Sin(angulo)));
        }

        var resultado = AjusteDeCirculo.Ajustar(pontos);

        resultado.Should().NotBeNull();
        resultado!.Value.Centro.X.Should().BeApproximately(0, 1e-6);
        resultado.Value.Centro.Y.Should().BeApproximately(0, 1e-6);
        resultado.Value.Raio.Should().BeApproximately(10, 1e-6);
    }
}
