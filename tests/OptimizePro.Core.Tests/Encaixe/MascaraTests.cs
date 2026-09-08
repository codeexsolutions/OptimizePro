using FluentAssertions;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Core.Tests.Encaixe;

public class MascaraTests
{
    [Fact]
    public void DeSilhueta_RetanguloCheio_RelevoCobreTodasAsLinhasEmTodaColuna()
    {
        var silhueta = new bool[3, 4];
        for (var c = 0; c < 3; c++)
            for (var l = 0; l < 4; l++)
                silhueta[c, l] = true;

        var m = Mascara.DeSilhueta(silhueta, raioDeFolga: 0);

        m.Colunas.Should().Be(3);
        m.Linhas.Should().Be(4);
        m.Topo.Should().AllSatisfy(t => t.Should().Be(0));
        m.Base.Should().AllSatisfy(b => b.Should().Be(3));
    }

    [Fact]
    public void DeSilhueta_FormaEmL_RelevoVariaPorColuna()
    {
        // Coluna 0: cheia da linha 0 à 3 (perna vertical do L).
        // Coluna 1 e 2: cheias só na linha 3 (base horizontal do L).
        var silhueta = new bool[3, 4];
        for (var l = 0; l < 4; l++) silhueta[0, l] = true;
        silhueta[1, 3] = true;
        silhueta[2, 3] = true;

        var m = Mascara.DeSilhueta(silhueta, raioDeFolga: 0);

        m.Topo.Should().Equal(0, 3, 3);
        m.Base.Should().Equal(3, 3, 3);
    }

    [Fact]
    public void DeSilhueta_ColunaVazia_TopoEBaseSaoMenosUm()
    {
        var silhueta = new bool[2, 3];
        silhueta[0, 1] = true;
        // coluna 1 inteira vazia

        var m = Mascara.DeSilhueta(silhueta, raioDeFolga: 0);

        m.Topo[1].Should().Be(-1);
        m.Base[1].Should().Be(-1);
    }

    [Fact]
    public void DeSilhueta_ComFolga_RelevoEngordaEmRelacaoAoSemFolga()
    {
        var silhueta = new bool[5, 5];
        silhueta[2, 2] = true; // um único pixel no centro

        var semFolga = Mascara.DeSilhueta(silhueta, raioDeFolga: 0);
        var comFolga = Mascara.DeSilhueta(silhueta, raioDeFolga: 1);

        semFolga.Topo[2].Should().Be(2);
        semFolga.Base[2].Should().Be(2);

        // com folga=1, a coluna 2 passa a cobrir linhas 1..3 (cruz Manhattan) e as
        // colunas 1 e 3 passam a ter uma célula cheia na linha 2.
        comFolga.Topo[2].Should().Be(1);
        comFolga.Base[2].Should().Be(3);
        comFolga.Topo[1].Should().Be(2);
        comFolga.Base[1].Should().Be(2);

        // o "desenho" (sem folga) continua igual — só o "cheio"/relevo muda.
        comFolga.ObterDesenho(2, 2).Should().Be(1);
        comFolga.ObterDesenho(1, 2).Should().Be(0);
    }

    [Fact]
    public void Construtor_TamanhoDeArrayIncompativel_Lanca()
    {
        var acao = () => new Mascara(2, 2, [1, 0], [1, 0], 0, 0); // devia ter 4 bytes, não 2

        acao.Should().Throw<ArgumentException>();
    }
}
