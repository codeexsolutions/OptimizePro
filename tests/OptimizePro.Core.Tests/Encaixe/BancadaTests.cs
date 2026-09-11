using FluentAssertions;
using OptimizePro.Core.Encaixe;
using Xunit;

namespace OptimizePro.Core.Tests.Encaixe;

public class BancadaTests
{
    [Fact]
    public void Empurrar_SemLimite_NaoMexe()
    {
        Bancada.Empurrar(y: 50, alturaEmCelulas: 30, linhasDaBancada: null).Should().Be(50);
    }

    [Fact]
    public void Empurrar_CabeNaBancadaAtual_NaoMexe()
    {
        // Bancada de 100 células; peça de altura 20 começando em 70 termina em 90 — cabe.
        Bancada.Empurrar(y: 70, alturaEmCelulas: 20, linhasDaBancada: 100).Should().Be(70);
    }

    [Fact]
    public void Empurrar_CruzariaALinha_EmpurraPraProximaBancada()
    {
        // Peça de altura 20 começando em 90 terminaria em 110 — cruza a linha dos 100.
        // Precisa ir pro começo da bancada seguinte: 100.
        Bancada.Empurrar(y: 90, alturaEmCelulas: 20, linhasDaBancada: 100).Should().Be(100);
    }

    [Fact]
    public void Empurrar_EncostaExatoNaLinha_NaoMexe()
    {
        // Termina exatamente em 100 — não CRUZA a linha, só encosta.
        Bancada.Empurrar(y: 80, alturaEmCelulas: 20, linhasDaBancada: 100).Should().Be(80);
    }

    [Fact]
    public void Empurrar_PecaMaiorQueABancada_AindaAssimEmpurraPraProximaLinha()
    {
        // Peça de 150 não cabe inteira em nenhuma bancada de 100 — o empurrão ainda assim
        // aplica a mesma regra (o motor decide separadamente se ela encaixa ou não).
        Bancada.Empurrar(y: 30, alturaEmCelulas: 150, linhasDaBancada: 100).Should().Be(100);
    }

    [Fact]
    public void Empurrar_EmCentimetros_MesmaRegraDaVersaoEmCelulas()
    {
        Bancada.Empurrar(y: 90.0, altura: 20.0, comprimentoBancadaCm: 100.0).Should().Be(100.0);
        Bancada.Empurrar(y: 70.0, altura: 20.0, comprimentoBancadaCm: 100.0).Should().Be(70.0);
        Bancada.Empurrar(y: 50.0, altura: 20.0, comprimentoBancadaCm: null).Should().Be(50.0);
    }

    [Theory]
    [InlineData(100.0, 0.2, 500)]
    [InlineData(100.5, 0.2, 502)] // arredonda pra baixo — bancada nunca fica MAIOR que o pedido.
    [InlineData(0.0, 0.2, null)]
    [InlineData(-5.0, 0.2, null)]
    public void EmCelulas_ConverteCorretamente(double? comprimentoCm, double passoCm, int? esperado)
    {
        Bancada.EmCelulas(comprimentoCm, passoCm).Should().Be(esperado);
    }

    [Fact]
    public void EmCelulas_SemLimite_RetornaNull()
    {
        Bancada.EmCelulas(null, 0.2).Should().BeNull();
    }
}
