using FluentAssertions;
using OptimizePro.Services.Impressoras;

namespace OptimizePro.Services.Tests.Impressoras;

public class TextoDeImpressorasTests
{
    [Theory]
    [InlineData("Cliente A - Malha PV.prt", "Cliente A", "Malha PV")]
    [InlineData("Cliente B – Viscose.cdr", "Cliente B", "Viscose")]
    [InlineData("Cliente_C_Suplex.prt", "Cliente", "C - Suplex")]
    [InlineData("ClienteD_Meia Malha.prt", "ClienteD", "Meia Malha")]
    [InlineData("SoNomeDoCliente.prt", "SoNomeDoCliente", "")]
    [InlineData("", "", "")]
    public void SepararClienteETecido_SeguindoAConvencaoDaFabrica(string tarefa, string clienteEsperado, string tecidoEsperado)
    {
        var (cliente, tecido) = TextoDeImpressoras.SepararClienteETecido(tarefa);
        cliente.Should().Be(clienteEsperado);
        tecido.Should().Be(tecidoEsperado);
    }

    [Fact]
    public void Normalizar_RemoveAcentoEDeixaMinusculo()
    {
        TextoDeImpressoras.Normalizar("REPOSIÇÃO").Should().Be("reposicao");
        TextoDeImpressoras.Normalizar("Reposicao").Should().Be("reposicao");
    }
}
