namespace OptimizePro.Core.Encaixe;

/// <summary>
/// Um trecho ocupado numa coluna do tecido, linha de início/fim inclusive (§21.3 da spec —
/// porte de <c>encaixarPorVaos</c>/<c>ocuparIntervalos</c> de <c>public/encaixe-motor.js</c>).
/// </summary>
public readonly record struct IntervaloOcupado(int Inicio, int Fim, int DonoId);

/// <summary>
/// O tecido guardado como a lista dos INTERVALOS ocupados de cada coluna — não como uma
/// altura só (o "relevo"/<c>perfil</c> do <see cref="EncaixadorPorContorno"/>). Com isso o
/// motor enxerga um vão que ficou ACIMA de uma peça já assentada, que o relevo simples perde
/// pra sempre assim que a peça é assentada (medido na referência: 18-31% do rolo em vão preso
/// em lotes reais).
/// </summary>
/// <remarks>
/// Guarda os dois mapas ao mesmo tempo, de propósito — não só o de intervalos: <see cref="Perfil"/>
/// (o relevo simples) e <see cref="MaiorVao"/> (o maior buraco FECHADO de cada coluna) juntos
/// são o que permite pular a descida cara pelos intervalos na esmagadora maioria das posições
/// (ver <see cref="EncaixadorPorVaos.MelhorVaga"/>) — só quando a peça realmente PODE parar
/// num buraco é que a varredura de intervalos roda de verdade. Medido na referência: essa
/// poda é 84% do custo do motor.
/// </remarks>
public sealed class TecidoPorVaos
{
    public int ColsTecido { get; }
    public List<IntervaloOcupado>[] Colunas { get; }
    public int[] Perfil { get; }

    /// <summary>Primeira linha livre de cada coluna — nem a descida pelos intervalos consegue pôr peça acima disto.</summary>
    public int[] TopoLivre { get; }

    /// <summary>Maior vão FECHADO (entre dois intervalos, ou entre o início do rolo e o primeiro) de cada coluna.</summary>
    public int[] MaiorVao { get; }

    public TecidoPorVaos(int colsTecido)
    {
        ColsTecido = colsTecido;
        Colunas = new List<IntervaloOcupado>[colsTecido];
        for (var c = 0; c < colsTecido; c++) Colunas[c] = [];
        Perfil = new int[colsTecido];
        TopoLivre = new int[colsTecido];
        MaiorVao = new int[colsTecido];
    }
}

/// <summary>
/// Porte de <c>encaixarPorVaos</c>/<c>melhorVagaPorVaos</c>/<c>descerNosVaos</c> (§21.3 da
/// spec) — o híbrido: silhueta real (como o contorno) + contabilidade de espaço livre por
/// intervalo (como uma caixa livre), então enxerga o vão que fica acima de uma peça já
/// assentada, sem jogar fora a forma de verdade.
/// </summary>
/// <remarks>
/// Escopo desta primeira versão: só peça avulsa (Agrupamento "Solta") — dupla/trio/cruzada
/// (blocos) ficam pro próximo incremento se medição real pedir; a máquina de blocos já
/// existente (<c>EncaixeService.ExecutarContorno</c>) não foi replicada aqui de propósito,
/// pra manter esta primeira versão do motor pequena e fácil de conferir. Sem as
/// "colunas-sonda" (poda extra da referência antes mesmo de calcular o relevo) — o atalho
/// "relevo primeiro, descida cara só onde há vão de verdade" já é, segundo a própria
/// referência, 84% do ganho; a poda de sondas fica pra depois se a performance real pedir.
/// </remarks>
public static class EncaixadorPorVaos
{
    private const int SaltoXPadrao = 3;
    private const int MaximoDeVoltas = 4096;

    /// <summary>
    /// Acha a melhor posição x/y para <paramref name="forma"/> sobre <paramref name="tecido"/>.
    /// Empate no fundo fica com o x mais à esquerda (mesma regra da referência — fecha o rolo
    /// por fileiras em vez de espalhar).
    /// </summary>
    public static PosicaoEncontrada? MelhorVaga(TecidoPorVaos tecido, Forma forma, int saltoX = SaltoXPadrao)
    {
        var colsTecido = tecido.ColsTecido;
        var xMax = colsTecido - forma.Colunas;
        if (xMax < 0) return null;

        PosicaoEncontrada? melhor = null;

        // Colunas com relevo válido cujo TOPO/BASE relativos ficam prontos uma vez, reaproveitados
        // por toda posição x testada — mesma ideia de cache-na-forma da referência
        // (`forma.alturaPorColuna`).
        var alturaPorColuna = new int[forma.Colunas];
        for (var c = 0; c < forma.Colunas; c++)
            alturaPorColuna[c] = forma.Topo[c] < 0 ? 0 : forma.Base[c] - forma.Topo[c] + 1;

        var colunasComVao = new int[forma.Colunas];

        void Avaliar(int x)
        {
            var ySky = 0;
            var piso = 0;
            var comVao = 0;
            var cortada = false;

            for (var c = 0; c < forma.Colunas; c++)
            {
                var t = forma.Topo[c];
                if (t < 0) continue;

                var coluna = x + c;
                var encosta = tecido.Perfil[coluna] - t;
                if (encosta > ySky) ySky = encosta;

                var cabeNoVao = alturaPorColuna[c] <= tecido.MaiorVao[coluna];
                if (cabeNoVao) colunasComVao[comVao++] = c;

                var livre = (cabeNoVao ? tecido.TopoLivre[coluna] : tecido.Perfil[coluna]) - t;
                if (livre > piso)
                {
                    piso = livre;
                    if (melhor is { } atual0 && piso + forma.MaxBase + 1 >= atual0.P1) { cortada = true; break; }
                }
            }
            if (cortada) return;

            var y = ySky;

            // O atalho que faz este motor caber no orçamento: o relevo simples e a descida cara
            // pelos intervalos dão o MESMO y quando não há vão de verdade nas colunas que a peça
            // cobre (a esmagadora maioria das posições — vão preso é exceção). Só desce de
            // verdade quando existe alguma coluna com vão E o piso ainda deixa margem abaixo do
            // relevo.
            if (comVao > 0 && piso < ySky)
            {
                var teto = melhor is { } atual1 ? (int)Math.Min(atual1.P1, y + forma.MaxBase + 1) : y + forma.MaxBase + 1;
                var yVao = DescerNosVaos(tecido, x, forma, alturaPorColuna, teto + 1, piso, colunasComVao, comVao);
                if (yVao is { } encontrado && encontrado < y) y = encontrado;
            }

            var fundo = y + forma.MaxBase + 1;
            if (melhor is null || fundo < melhor.Value.P1)
                melhor = new PosicaoEncontrada(x, y, fundo, 0);
        }

        if (saltoX <= 1)
        {
            for (var x = 0; x <= xMax; x++) Avaliar(x);
            return melhor;
        }

        for (var x = 0; x <= xMax; x += saltoX) Avaliar(x);
        if (xMax % saltoX != 0) Avaliar(xMax);

        if (melhor is { } m)
        {
            var inicio = Math.Max(0, m.X - (saltoX - 1));
            var fim = Math.Min(xMax, m.X + (saltoX - 1));
            for (var x = inicio; x <= fim; x++)
                if (x != m.X) Avaliar(x);
        }

        return melhor;
    }

    /// <summary>
    /// Desce <paramref name="forma"/> na coluna-base <paramref name="x"/> até o primeiro lugar em
    /// que nada bate — pode parar NO MEIO de um vão fechado por cima, diferente do relevo
    /// simples. Devolve <c>null</c> se a descida passar de <paramref name="tetoFundo"/> (não há o
    /// que ganhar dali pra baixo).
    /// </summary>
    private static int? DescerNosVaos(
        TecidoPorVaos tecido, int x, Forma forma, int[] alturaPorColuna, int tetoFundo, int deOnde,
        int[] colunasComVao, int quantasComVao)
    {
        var y = deOnde;

        for (var voltas = 0; voltas < MaximoDeVoltas; voltas++)
        {
            var proximo = y;

            for (var i = 0; i < quantasComVao; i++)
            {
                var c = colunasComVao[i];
                var t = forma.Topo[c];
                if (t < 0) continue;

                var lista = tecido.Colunas[x + c];
                if (lista.Count == 0) continue;

                var mudou = true;
                while (mudou)
                {
                    mudou = false;
                    var ini = proximo + t;
                    var fim = proximo + forma.Base[c];

                    // Primeiro intervalo cujo Fim já alcança a janela — busca binária (a lista
                    // está ordenada por Inicio e os intervalos não se sobrepõem, o que deixa Fim
                    // crescente também).
                    var baixo = 0;
                    var alto = lista.Count;
                    while (baixo < alto)
                    {
                        var meio = (baixo + alto) >> 1;
                        if (lista[meio].Fim < ini) baixo = meio + 1; else alto = meio;
                    }

                    if (baixo < lista.Count)
                    {
                        var iv = lista[baixo];
                        if (iv.Inicio <= fim && iv.Fim + 1 - t > proximo)
                        {
                            proximo = iv.Fim + 1 - t;
                            mudou = true;
                        }
                    }
                }
            }

            if (proximo == y) return y;
            y = proximo;
            if (y + forma.MaxBase + 1 >= tetoFundo) return null;
        }

        return null;
    }

    /// <summary>Marca <paramref name="forma"/> assentada em (<paramref name="x"/>,<paramref name="y"/>) — insere os intervalos e atualiza <see cref="TecidoPorVaos.Perfil"/>/<see cref="TecidoPorVaos.TopoLivre"/>/<see cref="TecidoPorVaos.MaiorVao"/>.</summary>
    public static void Ocupar(TecidoPorVaos tecido, Forma forma, int x, int y, int donoId)
    {
        for (var c = 0; c < forma.Colunas; c++)
        {
            var t = forma.Topo[c];
            if (t < 0) continue;

            var coluna = x + c;
            var lista = tecido.Colunas[coluna];

            var novo = new IntervaloOcupado(y + t, y + forma.Base[c], donoId);
            var indice = lista.FindIndex(iv => iv.Inicio > novo.Inicio);
            if (indice < 0) lista.Add(novo); else lista.Insert(indice, novo);

            var ate = y + forma.Base[c] + 1;
            if (ate > tecido.Perfil[coluna]) tecido.Perfil[coluna] = ate;

            AtualizarTopoLivreEMaiorVao(tecido, coluna);
        }
    }

    private static void AtualizarTopoLivreEMaiorVao(TecidoPorVaos tecido, int coluna)
    {
        var lista = tecido.Colunas[coluna];

        // Primeira linha livre: anda enquanto os intervalos se emendarem a partir do zero.
        var livre = 0;
        foreach (var iv in lista)
        {
            if (iv.Inicio > livre) break;
            if (iv.Fim + 1 > livre) livre = iv.Fim + 1;
        }
        tecido.TopoLivre[coluna] = livre;

        // O maior vão FECHADO: os buracos entre um intervalo e o seguinte, mais o que sobrou
        // entre o começo do rolo e o primeiro intervalo — todos servem de vaga.
        var maior = 0;
        var fimAtual = -1;
        foreach (var iv in lista)
        {
            var vao = iv.Inicio - (fimAtual + 1);
            if (vao > maior) maior = vao;
            if (iv.Fim > fimAtual) fimAtual = iv.Fim;
        }
        tecido.MaiorVao[coluna] = maior;
    }

    /// <summary>
    /// Laço externo — mesma forma de <see cref="EncaixadorPorContorno.Encaixar"/>: assenta cada
    /// item da <paramref name="ordem"/> na melhor vaga que <see cref="MelhorVaga"/> achar.
    /// </summary>
    public static ResultadoContorno Encaixar(int colsTecido, IReadOnlyList<(ItemEncaixe Item, int RotacaoGraus)> ordem)
    {
        var tecido = new TecidoPorVaos(colsTecido);
        var posicoes = new List<PosicaoDeItem>();
        var naoEncaixados = new List<string>();
        var fundoMaximo = 0;

        for (var i = 0; i < ordem.Count; i++)
        {
            var (item, rotacao) = ordem[i];
            if (!item.MascarasPorRotacao.TryGetValue(rotacao, out var mascara))
                throw new ArgumentException($"Item '{item.Id}' não tem máscara para a rotação {rotacao}°.");

            var forma = Forma.DeMascaraUnica(mascara);

            var escolha = forma.Colunas <= colsTecido ? MelhorVaga(tecido, forma) : null;
            if (escolha is not { } pos)
            {
                naoEncaixados.Add(item.Id);
                continue;
            }

            Ocupar(tecido, forma, pos.X, pos.Y, i);

            var fundoDaForma = pos.Y + forma.MaxBase + 1;
            if (fundoDaForma > fundoMaximo) fundoMaximo = fundoDaForma;

            posicoes.Add(new PosicaoDeItem(item.Id, rotacao, pos.X, pos.Y));
        }

        return new ResultadoContorno(posicoes, naoEncaixados, fundoMaximo);
    }
}
