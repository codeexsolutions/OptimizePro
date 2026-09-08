using FluentAssertions;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Core.Tests.Encaixe;

public class GiroTests
{
    [Theory]
    [InlineData(TipoDeGiro.MantemSentido, new[] { 0, 180 })]
    [InlineData(TipoDeGiro.Fixa, new[] { 0 })]
    [InlineData(TipoDeGiro.Livre, new[] { 0, 90, 180, 270 })]
    public void RotacoesPara_RetornaOConjuntoCorretoPorTipo(TipoDeGiro tipo, int[] esperado)
    {
        Giro.RotacoesPara(tipo).Should().Equal(esperado);
    }
}
