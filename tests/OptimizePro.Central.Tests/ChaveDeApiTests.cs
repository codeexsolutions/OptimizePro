using FluentAssertions;

namespace OptimizePro.Central.Tests;

public class ChaveDeApiTests
{
    [Fact]
    public void Gerar_ProduzChavesDiferentesACadaChamada()
    {
        var a = ChaveDeApi.Gerar();
        var b = ChaveDeApi.Gerar();
        a.Should().NotBe(b);
    }

    [Fact]
    public void Conferir_ChaveCorreta_Confere()
    {
        var chave = ChaveDeApi.Gerar();
        var hash = ChaveDeApi.Hash(chave);
        ChaveDeApi.Conferir(chave, hash).Should().BeTrue();
    }

    [Fact]
    public void Conferir_ChaveErrada_NaoConfere()
    {
        var hash = ChaveDeApi.Hash(ChaveDeApi.Gerar());
        ChaveDeApi.Conferir("chave-qualquer-errada", hash).Should().BeFalse();
    }
}
