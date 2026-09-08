namespace OptimizePro.Core.Encaixe;

/// <summary>
/// Porte de <c>encostarNaForma</c> (§11.8): testa todo deslocamento horizontal possível de
/// uma peça nova relativa a um bloco já montado, escolhe o que produz o menor retângulo
/// envolvente — igual ao "manga com manga invertida fecha quase um retângulo".
/// </summary>
public static class EncostoDeFormas
{
    /// <summary>
    /// Encosta <paramref name="novaMascara"/> em <paramref name="bloco"/>, testando todo
    /// deslocamento horizontal (da encostada totalmente à esquerda até totalmente à
    /// direita, sem sobrepor) e devolvendo a combinação de menor área envolvente.
    /// </summary>
    public static Forma EncostarNaForma(Forma bloco, Mascara novaMascara, int rotacaoGraus = 0)
    {
        var c0 = bloco.Colunas;
        var cn = novaMascara.Colunas;

        Forma? melhor = null;
        var melhorArea = double.MaxValue;

        for (var d = -cn; d <= c0; d++)
        {
            var minCol = Math.Min(0, d);
            var maxCol = Math.Max(c0 - 1, d + cn - 1);
            var larguraCombinada = maxCol - minCol + 1;

            var y = CalcularEncosto(bloco, novaMascara, d);
            var maxBaseNova = MaxBaseDaMascara(novaMascara);
            var alturaCombinada = Math.Max(bloco.MaxBase, y + maxBaseNova) + 1;

            var area = (double)larguraCombinada * alturaCombinada;
            if (area >= melhorArea) continue;

            melhorArea = area;
            melhor = ConstruirFormaCombinada(bloco, novaMascara, d, y, minCol, larguraCombinada, rotacaoGraus);
        }

        return melhor!; // d sempre tem pelo menos uma opção (o range nunca é vazio)
    }

    /// <summary>Deslocamento vertical mínimo para a peça nova não sobrepor o bloco nas colunas onde há sobreposição horizontal.</summary>
    private static int CalcularEncosto(Forma bloco, Mascara novaMascara, int d)
    {
        var y = 0;

        for (var cNovo = 0; cNovo < novaMascara.Colunas; cNovo++)
        {
            var cBloco = cNovo + d;
            if (cBloco < 0 || cBloco >= bloco.Colunas) continue;
            if (novaMascara.Topo[cNovo] < 0 || bloco.Topo[cBloco] < 0) continue;

            var encosta = bloco.Base[cBloco] + 1 - novaMascara.Topo[cNovo];
            if (encosta > y) y = encosta;
        }

        return y;
    }

    private static int MaxBaseDaMascara(Mascara mascara)
    {
        var maxBase = -1;
        for (var c = 0; c < mascara.Colunas; c++)
            if (mascara.Base[c] > maxBase) maxBase = mascara.Base[c];
        return maxBase;
    }

    private static Forma ConstruirFormaCombinada(Forma bloco, Mascara novaMascara, int d, int y, int minCol, int largura, int rotacaoGraus)
    {
        var topo = new int[largura];
        var baseArr = new int[largura];
        Array.Fill(topo, -1);
        Array.Fill(baseArr, -1);

        for (var c = 0; c < bloco.Colunas; c++)
        {
            var destino = c - minCol;
            topo[destino] = bloco.Topo[c];
            baseArr[destino] = bloco.Base[c];
        }

        for (var c = 0; c < novaMascara.Colunas; c++)
        {
            if (novaMascara.Topo[c] < 0) continue;

            var destino = c + d - minCol;
            var topoNovo = novaMascara.Topo[c] + y;
            var baseNovo = novaMascara.Base[c] + y;

            topo[destino] = topo[destino] < 0 ? topoNovo : Math.Min(topo[destino], topoNovo);
            baseArr[destino] = Math.Max(baseArr[destino], baseNovo);
        }

        List<ParteDaForma> partes =
        [
            .. bloco.Partes.Select(p => p with { DeslocamentoColuna = p.DeslocamentoColuna - minCol }),
            new ParteDaForma(d - minCol, novaMascara, y, rotacaoGraus),
        ];

        return new Forma(largura, topo, baseArr, partes);
    }
}
