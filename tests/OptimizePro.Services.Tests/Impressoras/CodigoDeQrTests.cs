using FluentAssertions;
using OptimizePro.Services.Impressoras;

namespace OptimizePro.Services.Tests.Impressoras;

public class CodigoDeQrTests
{
    [Fact]
    public void GerarCurto_MesmoIdMesmoPrefixo_SempreOMesmoCodigo()
    {
        var a = CodigoDeQr.GerarCurto(CodigoDeQr.PrefixoRegistro, "abc123");
        var b = CodigoDeQr.GerarCurto(CodigoDeQr.PrefixoRegistro, "abc123");
        a.Should().Be(b);
        a.Should().StartWith("R");
        a.Should().HaveLength(11);
    }

    [Fact]
    public void GerarCurto_PrefixoDiferente_CodigoDiferente()
    {
        var registro = CodigoDeQr.GerarCurto(CodigoDeQr.PrefixoRegistro, "abc123");
        var pedido = CodigoDeQr.GerarCurto(CodigoDeQr.PrefixoPedido, "abc123");
        registro.Should().NotBe(pedido);
        pedido.Should().StartWith("P");
    }

    [Fact]
    public void GerarCurto_IdsDiferentes_CodigosDiferentes()
    {
        var a = CodigoDeQr.GerarCurto(CodigoDeQr.PrefixoRegistro, "abc123");
        var b = CodigoDeQr.GerarCurto(CodigoDeQr.PrefixoRegistro, "xyz789");
        a.Should().NotBe(b);
    }
}
