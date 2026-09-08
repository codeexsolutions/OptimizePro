namespace OptimizePro.Core.Encaixe;

/// <summary>
/// Uma peça dentro de uma <see cref="Forma"/> (isolada ou parte de um bloco, §11.8), com seu
/// deslocamento coluna/linha relativo ao canto da forma combinada. <see cref="RotacaoGraus"/>
/// precisa vir explícito (não dá pra descobrir comparando <see cref="Mascara"/> por referência
/// contra as máscaras cacheadas do item: <see cref="RotacaoDeMascara.Rotacionar"/> só devolve a
/// mesma instância pra 0° — 180°/90°/270° sempre alocam objeto novo, então duas rotações de
/// mesmo ângulo mas origens de chamada diferentes nunca são o mesmo objeto).
/// </summary>
public sealed record ParteDaForma(int DeslocamentoColuna, Mascara Mascara, int DeslocamentoLinha = 0, int RotacaoGraus = 0);

/// <summary>
/// Porte de <c>Forma</c> (§11.2) — unidade posicionável pelo encaixador de contorno.
/// Peça isolada (<see cref="DeMascaraUnica"/>) ou bloco de peças combinadas (dupla/trio/
/// quarteto, §11.8, ver <see cref="AgrupamentoDeBlocos"/>) — em ambos os casos, um único
/// relevo topo/base pronto para posicionar.
/// </summary>
public sealed class Forma
{
    public int Colunas { get; }
    public IReadOnlyList<int> Topo { get; }
    public IReadOnlyList<int> Base { get; }
    public IReadOnlyList<ParteDaForma> Partes { get; }

    /// <summary>Número de colunas com relevo válido (topo≥0) — <c>nCols</c> na especificação.</summary>
    public int NumeroDeColunasValidas { get; }

    public long SomaTopo { get; }
    public int MaxBase { get; }

    public Forma(int colunas, IReadOnlyList<int> topo, IReadOnlyList<int> baseArr, IReadOnlyList<ParteDaForma> partes)
    {
        Colunas = colunas;
        Topo = topo;
        Base = baseArr;
        Partes = partes;

        var nCols = 0;
        long somaTopo = 0;
        var maxBase = -1;

        for (var c = 0; c < colunas; c++)
        {
            if (topo[c] < 0) continue;
            nCols++;
            somaTopo += topo[c];
            if (baseArr[c] > maxBase) maxBase = baseArr[c];
        }

        NumeroDeColunasValidas = nCols;
        SomaTopo = somaTopo;
        MaxBase = maxBase;
    }

    public static Forma DeMascaraUnica(Mascara mascara, int rotacaoGraus = 0) =>
        new(mascara.Colunas, mascara.Topo, mascara.Base, [new ParteDaForma(0, mascara, RotacaoGraus: rotacaoGraus)]);
}
