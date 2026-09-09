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
    public void Dilatar_Raio0_DevolveOMesmoConteudo()
    {
        var origem = GradeVazia(5, 5);
        origem[2, 2] = true;

        var resultado = Dilatacao.Dilatar(origem, 0);

        Contar(resultado).Should().Be(1);
        resultado[2, 2].Should().BeTrue();
    }

    [Fact]
    public void Dilatar_Raio1_FormaCruzDe5Celulas()
    {
        // dx²+dy²<=1 só cabe a cruz — coincide com o diamante de Manhattan neste raio.
        var origem = GradeVazia(5, 5);
        origem[2, 2] = true;

        var resultado = Dilatacao.Dilatar(origem, 1);

        Contar(resultado).Should().Be(5);
        resultado[2, 2].Should().BeTrue();
        resultado[1, 2].Should().BeTrue();
        resultado[3, 2].Should().BeTrue();
        resultado[2, 1].Should().BeTrue();
        resultado[2, 3].Should().BeTrue();
        resultado[1, 1].Should().BeFalse(); // diagonal — ainda fora do disco neste raio pequeno
    }

    [Fact]
    public void Dilatar_Raio2_FormaDiscoDe13Celulas()
    {
        // dx²+dy²<=4 dá 13 células — neste raio o disco ainda coincide com o diamante antigo
        // (só diverge a partir de r=3), mas já vale conferir a contagem exata.
        var origem = GradeVazia(7, 7);
        origem[3, 3] = true;

        var resultado = Dilatacao.Dilatar(origem, 2);

        Contar(resultado).Should().Be(13);
    }

    /// <summary>
    /// Regressão (02/09/2026) — a partir de r=3 o disco euclidiano inclui pontos em diagonal
    /// que o diamante de Manhattan antigo excluía (ex.: (2,2), com distância euclidiana ≈2,83
    /// ≤ 3, mas soma Manhattan 4 > 3). O diamante antigo SUBDIMENSIONAVA a margem exatamente
    /// no contato diagonal — o lado ruim do erro, porque a peça vizinha podia encostar.
    /// </summary>
    [Fact]
    public void Dilatar_Raio3_IncluiCantoDiagonalQueDiamanteAntigoExcluiria()
    {
        var origem = GradeVazia(9, 9);
        origem[4, 4] = true;

        var resultado = Dilatacao.Dilatar(origem, 3);

        resultado[6, 6].Should().BeTrue("dx=2,dy=2 tem distância euclidiana √8≈2,83 ≤ 3 (dentro do disco)");
        resultado[7, 7].Should().BeFalse("dx=3,dy=3 tem distância euclidiana √18≈4,24 > 3 (fora do disco)");
    }

    [Fact]
    public void Dilatar_PertoDaBorda_NaoEstouraOLimite()
    {
        var origem = GradeVazia(3, 3);
        origem[0, 0] = true; // canto

        var resultado = Dilatacao.Dilatar(origem, 1);

        // canto tem só 2 vizinhos válidos (direita e abaixo) + o próprio = 3 células.
        Contar(resultado).Should().Be(3);
        resultado[0, 0].Should().BeTrue();
        resultado[1, 0].Should().BeTrue();
        resultado[0, 1].Should().BeTrue();
    }
}
