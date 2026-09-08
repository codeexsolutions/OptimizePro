using FluentAssertions;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Core.Tests.Encaixe;

public class FormaTests
{
    [Fact]
    public void DeMascaraUnica_CalculaEstatisticasDoRelevoCorretamente()
    {
        // Coluna 0: topo=0,base=3 (relevo cheio). Coluna 1: vazia. Coluna 2: topo=1,base=2.
        var silhueta = new bool[3, 4];
        for (var l = 0; l < 4; l++) silhueta[0, l] = true;
        silhueta[2, 1] = true;
        silhueta[2, 2] = true;

        var mascara = Mascara.DeSilhueta(silhueta, raioDeFolga: 0);
        var forma = Forma.DeMascaraUnica(mascara);

        forma.Colunas.Should().Be(3);
        forma.NumeroDeColunasValidas.Should().Be(2); // só colunas 0 e 2 têm topo>=0
        forma.SomaTopo.Should().Be(0 + 1);
        forma.MaxBase.Should().Be(3);
        forma.Partes.Should().HaveCount(1);
        forma.Partes[0].Mascara.Should().BeSameAs(mascara);
    }
}
