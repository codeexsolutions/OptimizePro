using FluentAssertions;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Core.Tests.Encaixe;

/// <summary>
/// Prova de equivalência bit-a-bit entre <see cref="EncaixadorPorContorno.MelhorPosicaoDaUnidadeV2"/>
/// (SIMD, experimental) e a V1 original — condição pra V2 ser usada em produção (§9.3): mesmo
/// resultado matemático sempre, só mais rápido. Gera formas/perfis aleatórios (com "buracos"
/// Topo&lt;0, como formas reais côncavas têm) e compara as duas em centenas de combinações.
/// </summary>
public class EncaixadorPorContornoV2Tests
{
    private static Forma FormaAleatoria(Random r, int colunas)
    {
        var topo = new int[colunas];
        var baseArr = new int[colunas];

        for (var c = 0; c < colunas; c++)
        {
            if (r.NextDouble() < 0.15) // ~15% de colunas "buraco" (Topo<0), como cavas/recortes reais.
            {
                topo[c] = -1;
                baseArr[c] = -1;
            }
            else
            {
                topo[c] = r.Next(0, 8);
                baseArr[c] = topo[c] + r.Next(0, 15);
            }
        }

        // Garante ao menos 1 coluna válida (senão MaxBase fica -1 e a forma não faz sentido).
        if (topo.All(t => t < 0))
        {
            topo[0] = 0;
            baseArr[0] = 5;
        }

        return new Forma(colunas, topo, baseArr, []);
    }

    private static int[] PerfilAleatorio(Random r, int colsTecido) =>
        [.. Enumerable.Range(0, colsTecido).Select(_ => r.Next(0, 50))];

    [Theory]
    [InlineData(HeuristicaDeContorno.Fundo, 1)]
    [InlineData(HeuristicaDeContorno.Fundo, 3)]
    [InlineData(HeuristicaDeContorno.Vazio, 1)]
    [InlineData(HeuristicaDeContorno.Vazio, 3)]
    public void MelhorPosicaoDaUnidadeV2_MesmoResultadoQueV1_EmCentenasDeCombinacoesAleatorias(HeuristicaDeContorno heuristica, int saltoX)
    {
        var r = new Random(12345 + (int)heuristica * 10 + saltoX);

        for (var tentativa = 0; tentativa < 300; tentativa++)
        {
            var colunasDaForma = r.Next(1, 40);
            var colsTecido = colunasDaForma + r.Next(0, 60);

            var forma = FormaAleatoria(r, colunasDaForma);
            var perfil = PerfilAleatorio(r, colsTecido);

            var v1 = EncaixadorPorContorno.MelhorPosicaoDaUnidade(perfil, colsTecido, forma, heuristica, saltoX);
            var v2 = EncaixadorPorContorno.MelhorPosicaoDaUnidadeV2(perfil, colsTecido, forma, heuristica, saltoX);

            v2.Should().Be(v1, because: $"tentativa {tentativa}: colunasDaForma={colunasDaForma}, colsTecido={colsTecido}");
        }
    }

    [Theory]
    [InlineData(HeuristicaDeContorno.Fundo)]
    [InlineData(HeuristicaDeContorno.Vazio)]
    public void MelhorPosicaoDaUnidadeV2_FormaMenorQueUmVetorSimd_AindaBateComV1(HeuristicaDeContorno heuristica)
    {
        // Formas bem pequenas (menores que Vector<int>.Count) só passam pela cauda escalar —
        // caso de borda que o teste aleatório acima pode não cobrir sempre (colunasDaForma
        // sorteado de 1 a 39, mas vale garantir explicitamente aqui).
        var r = new Random(999);
        for (var colunas = 1; colunas <= 3; colunas++)
        {
            var forma = FormaAleatoria(r, colunas);
            var perfil = PerfilAleatorio(r, colunas + 10);

            var v1 = EncaixadorPorContorno.MelhorPosicaoDaUnidade(perfil, colunas + 10, forma, heuristica);
            var v2 = EncaixadorPorContorno.MelhorPosicaoDaUnidadeV2(perfil, colunas + 10, forma, heuristica);

            v2.Should().Be(v1);
        }
    }

    [Fact]
    public void MelhorPosicaoDaUnidadeV2_NaoCabeNoTecido_DevolveNuloIgualAV1()
    {
        var forma = new Forma(10, [0, 1, 2, 3, 4, 3, 2, 1, 0, 0], [5, 6, 7, 8, 9, 8, 7, 6, 5, 5], []);
        var perfil = new int[5]; // colsTecido menor que a forma.

        EncaixadorPorContorno.MelhorPosicaoDaUnidadeV2(perfil, 5, forma, HeuristicaDeContorno.Fundo).Should().BeNull();
    }
}
