using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Encaixe.Nfp;

namespace OptimizePro.Core.Tests.Encaixe.Nfp;

public class CalculadoraDeNfpTests
{
    [Fact]
    public void Calcular_DoisRetangulos_NfpTemDimensaoIgualASomaDosLados()
    {
        // Soma de Minkowski de dois retângulos alinhados aos eixos é um retângulo cuja
        // largura/altura são a soma das larguras/alturas dos dois originais.
        List<PontoXY> a = [new(0, 0), new(2, 0), new(2, 2), new(0, 2)];
        List<PontoXY> b = [new(0, 0), new(3, 0), new(3, 1), new(0, 1)];

        var nfp = CalculadoraDeNfp.Calcular(a, b);

        nfp.Should().HaveCount(1);
        var caixa = Geometria.CaixaDeContorno(nfp[0]);
        caixa.Largura.Should().BeApproximately(5.0, 1e-6);
        caixa.Altura.Should().BeApproximately(3.0, 1e-6);
    }

    [Fact]
    public void Calcular_MesmoQuadradoComEleMesmo_NfpDobraDeTamanho()
    {
        List<PontoXY> quadrado = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)];

        var nfp = CalculadoraDeNfp.Calcular(quadrado, quadrado);

        nfp.Should().HaveCount(1);
        var caixa = Geometria.CaixaDeContorno(nfp[0]);
        caixa.Largura.Should().BeApproximately(8.0, 1e-6);
        caixa.Altura.Should().BeApproximately(8.0, 1e-6);
    }

    [Fact]
    public void Calcular_RetangulosEmSentidoHorario_FuncionaIgualAoAntiHorario()
    {
        // O algoritmo precisa normalizar orientação sozinho — entrada em sentido horário
        // deve dar o mesmo resultado que a mesma forma em sentido anti-horário.
        List<PontoXY> aHorario = [new(0, 0), new(0, 2), new(2, 2), new(2, 0)];
        List<PontoXY> b = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

        var nfp = CalculadoraDeNfp.Calcular(aHorario, b);

        nfp.Should().HaveCount(1);
        var caixa = Geometria.CaixaDeContorno(nfp[0]);
        caixa.Largura.Should().BeApproximately(3.0, 1e-6);
        caixa.Altura.Should().BeApproximately(3.0, 1e-6);
    }
}
