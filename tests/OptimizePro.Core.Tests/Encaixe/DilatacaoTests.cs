using FluentAssertions;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Core.Tests.Encaixe;

public class DilatacaoTests
{
    private static bool[,] GradeVazia(int cols, int linhas) => new bool[cols, linhas];

    private static int Contar(bool[,] g)
    {
        var n = 0;
        for (var c = 0; c < g.GetLength(0); c++)
            for (var l = 0; l < g.GetLength(1); l++)
                if (g[c, l]) n++;
        return n;
    }

    [Fact]
    public void DilatarManhattan_Raio0_DevolveOMesmoConteudo()
    {
        var origem = GradeVazia(5, 5);
        origem[2, 2] = true;

        var resultado = Dilatacao.DilatarManhattan(origem, 0);

        Contar(resultado).Should().Be(1);
        resultado[2, 2].Should().BeTrue();
    }

    [Fact]
    public void DilatarManhattan_Raio1_FormaCruzDe5Celulas()
    {
        var origem = GradeVazia(5, 5);
        origem[2, 2] = true;

        var resultado = Dilatacao.DilatarManhattan(origem, 1);

        Contar(resultado).Should().Be(5);
        resultado[2, 2].Should().BeTrue();
        resultado[1, 2].Should().BeTrue();
        resultado[3, 2].Should().BeTrue();
        resultado[2, 1].Should().BeTrue();
        resultado[2, 3].Should().BeTrue();
        resultado[1, 1].Should().BeFalse(); // diagonal — fora do diamante Manhattan
    }

    [Fact]
    public void DilatarManhattan_Raio2_FormaDiamanteDe13Celulas()
    {
        // |dx|+|dy|<=r tem 2r²+2r+1 células; para r=2: 8+4+1=13.
        var origem = GradeVazia(7, 7);
        origem[3, 3] = true;

        var resultado = Dilatacao.DilatarManhattan(origem, 2);

        Contar(resultado).Should().Be(13);
    }

    [Fact]
    public void DilatarManhattan_PertoDaBorda_NaoEstouraOLimite()
    {
        var origem = GradeVazia(3, 3);
        origem[0, 0] = true; // canto

        var resultado = Dilatacao.DilatarManhattan(origem, 1);

        // canto tem só 2 vizinhos válidos (direita e abaixo) + o próprio = 3 células.
        Contar(resultado).Should().Be(3);
        resultado[0, 0].Should().BeTrue();
        resultado[1, 0].Should().BeTrue();
        resultado[0, 1].Should().BeTrue();
    }
}
