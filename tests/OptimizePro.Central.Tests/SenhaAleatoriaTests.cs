using FluentAssertions;

namespace OptimizePro.Central.Tests;

public class SenhaAleatoriaTests
{
    [Fact]
    public void Gerar_ProduzSenhasDiferentesACadaChamada()
    {
        var a = SenhaAleatoria.Gerar();
        var b = SenhaAleatoria.Gerar();
        a.Should().NotBe(b);
    }

    [Fact]
    public void Gerar_RespeitaOTamanhoPedido()
    {
        SenhaAleatoria.Gerar(14).Should().HaveLength(14);
    }

    [Fact]
    public void Gerar_SemCaracteresAmbiguos()
    {
        var senha = SenhaAleatoria.Gerar(200);
        senha.Should().NotContainAny("0", "O", "1", "l", "I");
    }
}
