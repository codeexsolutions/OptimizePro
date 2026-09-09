namespace OptimizePro.Core.Encaixe;

/// <summary>
/// Porte de <c>Mascara</c> (§11.2) — silhueta de uma peça (numa rotação) representada
/// como "relevo" por coluna (<see cref="Topo"/>/<see cref="Base"/>), a estrutura central
/// usada pelo encaixador de contorno. Linha 0 = borda de ataque da peça (a que encosta
/// primeiro ao ser assentada, mais próxima do início do rolo).
/// </summary>
public sealed class Mascara
{
    public int Colunas { get; }
    public int Linhas { get; }

    /// <summary>Primeira linha cheia de cada coluna, na silhueta COM folga; -1 = coluna vazia.</summary>
    public IReadOnlyList<int> Topo { get; }

    /// <summary>Última linha cheia de cada coluna, na silhueta COM folga; -1 = coluna vazia.</summary>
    public IReadOnlyList<int> Base { get; }

    /// <summary>Silhueta sem folga (desenho real, para renderização) — cols*linhas, 0/1.</summary>
    public IReadOnlyList<byte> Desenho { get; }

    /// <summary>Silhueta com folga (usada para posicionar — o "engorde" já embute a folga pedida).</summary>
    public IReadOnlyList<byte> Cheio { get; }

    public double OffXCm { get; }
    public double OffYCm { get; }

    public Mascara(int colunas, int linhas, byte[] desenho, byte[] cheio, double offXCm = 0, double offYCm = 0)
    {
        if (desenho.Length != colunas * linhas)
            throw new ArgumentException("desenho.Length precisa ser colunas*linhas.", nameof(desenho));
        if (cheio.Length != colunas * linhas)
            throw new ArgumentException("cheio.Length precisa ser colunas*linhas.", nameof(cheio));

        Colunas = colunas;
        Linhas = linhas;
        Desenho = desenho;
        Cheio = cheio;
        OffXCm = offXCm;
        OffYCm = offYCm;

        (Topo, Base) = CalcularRelevo(cheio, colunas, linhas);
    }

    /// <summary>Constrói a partir de uma silhueta booleana crua, aplicando a dilatação da folga (§11.2).</summary>
    public static Mascara DeSilhueta(bool[,] silhuetaSemFolga, int raioDeFolga, double offXCm = 0, double offYCm = 0)
    {
        var cols = silhuetaSemFolga.GetLength(0);
        var linhas = silhuetaSemFolga.GetLength(1);
        var comFolga = raioDeFolga > 0 ? Dilatacao.Dilatar(silhuetaSemFolga, raioDeFolga) : silhuetaSemFolga;

        return new Mascara(cols, linhas, ParaBytes(silhuetaSemFolga, cols, linhas), ParaBytes(comFolga, cols, linhas), offXCm, offYCm);
    }

    public byte ObterCheio(int coluna, int linha) => Cheio[coluna * Linhas + linha];

    public byte ObterDesenho(int coluna, int linha) => Desenho[coluna * Linhas + linha];

    private static (int[] Topo, int[] Base) CalcularRelevo(byte[] cheio, int cols, int linhas)
    {
        var topo = new int[cols];
        var baseArr = new int[cols];

        for (var c = 0; c < cols; c++)
        {
            var primeira = -1;
            var ultima = -1;

            for (var l = 0; l < linhas; l++)
            {
                if (cheio[c * linhas + l] == 0) continue;
                if (primeira < 0) primeira = l;
                ultima = l;
            }

            topo[c] = primeira;
            baseArr[c] = ultima;
        }

        return (topo, baseArr);
    }

    private static byte[] ParaBytes(bool[,] grade, int cols, int linhas)
    {
        var resultado = new byte[cols * linhas];
        for (var c = 0; c < cols; c++)
            for (var l = 0; l < linhas; l++)
                resultado[c * linhas + l] = grade[c, l] ? (byte)1 : (byte)0;
        return resultado;
    }
}
