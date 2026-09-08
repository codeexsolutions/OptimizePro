namespace OptimizePro.Core.Encaixe.Busca;

/// <summary>
/// "Explorar" parte da lista crua com embaralhar forte; "refinar" parte da melhor ordem já
/// encontrada com embaralhar leve (§11.7).
/// </summary>
/// <remarks>
/// A especificação não define exatamente o quanto é "leve" — implementado como uma fração
/// pequena (10%) de trocas aleatórias sobre a ordem base, preservando a maior parte dela;
/// ajustar se a busca real precisar de mais/menos perturbação.
/// </remarks>
public static class Embaralhamento
{
    public const double FracaoDeTrocasLeve = 0.1;

    public static int[] Forte(int quantidade, Random aleatorio)
    {
        var ordem = Enumerable.Range(0, quantidade).ToArray();

        for (var i = ordem.Length - 1; i > 0; i--)
        {
            var j = aleatorio.Next(i + 1);
            (ordem[i], ordem[j]) = (ordem[j], ordem[i]);
        }

        return ordem;
    }

    public static int[] Leve(IReadOnlyList<int> ordemBase, Random aleatorio, double fracaoDeTrocas = FracaoDeTrocasLeve)
    {
        var ordem = ordemBase.ToArray();
        if (ordem.Length < 2)
            return ordem;

        var trocas = Math.Max(1, (int)Math.Round(ordem.Length * fracaoDeTrocas));

        for (var k = 0; k < trocas; k++)
        {
            var i = aleatorio.Next(ordem.Length);
            var j = aleatorio.Next(ordem.Length);
            (ordem[i], ordem[j]) = (ordem[j], ordem[i]);
        }

        return ordem;
    }

    /// <summary>Fração do FIM da ordem que <see cref="ReconstruirRabo"/> embaralha — o resto (a "cabeça") fica intocado.</summary>
    public const double FracaoDoRaboPadrao = 0.2;

    /// <summary>
    /// "Ruin and recreate" (§9.3, item 3 do plano de melhoria de aproveitamento — pegar o
    /// melhor resultado já achado e reconstruir só um PEDAÇO dele, não a busca inteira).
    /// Diferente de <see cref="Leve"/> (troca aleatória em QUALQUER posição da ordem, sem saber
    /// se ali o encaixe já estava bom ou ruim), esta embaralha só o TRECHO FINAL da melhor
    /// ordem já encontrada — no motor bottom-left/relevo, os últimos itens colocados tendem a
    /// ser justamente os que definem o fundo mais alto (o <c>consumo</c> final), então é ali que
    /// uma reordenação tem mais chance real de render — a "cabeça" (parte já bem assentada)
    /// nunca é mexida.
    /// </summary>
    public static int[] ReconstruirRabo(IReadOnlyList<int> ordemBase, Random aleatorio, double fracaoDoRabo = FracaoDoRaboPadrao)
    {
        var ordem = ordemBase.ToArray();
        if (ordem.Length < 2)
            return ordem;

        var tamanhoDoRabo = Math.Max(1, (int)Math.Round(ordem.Length * fracaoDoRabo));
        var inicioDoRabo = Math.Max(0, ordem.Length - tamanhoDoRabo);

        // Fisher-Yates restrito ao trecho [inicioDoRabo, fim) — a cabeça fica exatamente igual.
        for (var i = ordem.Length - 1; i > inicioDoRabo; i--)
        {
            var j = inicioDoRabo + aleatorio.Next(i - inicioDoRabo + 1);
            (ordem[i], ordem[j]) = (ordem[j], ordem[i]);
        }

        return ordem;
    }

    /// <summary>
    /// "Reparo guiado" (02/09/2026, porte de <c>repararPior</c> do projeto de referência,
    /// §11.7) — em vez de sacudir a ordem sem direção, mexe só na unidade que se SABE que
    /// ficou mal na última tentativa (a que sobrou mais buraco morto, ver <c>ResultadoMotor.PiorUnidadeItens</c>
    /// em <c>EncaixeService</c>). Tira os índices de <paramref name="itensDaPiorUnidade"/> de
    /// onde estão (preservando a ordem relativa entre eles, já que formavam uma unidade só —
    /// dupla/trio/cruzada) e devolve o bloco inteiro mais cedo na fila, num lugar sorteado entre
    /// o começo e a posição em que ele estava. Entrar mais cedo dá a ele a chance de escolher
    /// uma posição melhor, antes que o relevo do tecido já esteja mais ocupado.
    /// </summary>
    /// <remarks>
    /// Se nenhum dos índices de <paramref name="itensDaPiorUnidade"/> estiver em
    /// <paramref name="ordemBase"/> (não deveria acontecer — mesma quantidade de itens sempre
    /// nesta busca — mas defensivo), devolve uma cópia da ordem como está.
    /// </remarks>
    public static int[] RepararPior(IReadOnlyList<int> ordemBase, IReadOnlyList<int> itensDaPiorUnidade, Random aleatorio)
    {
        var ordem = ordemBase.ToArray();
        if (itensDaPiorUnidade.Count == 0)
            return ordem;

        var alvo = new HashSet<int>(itensDaPiorUnidade);
        var restante = new List<int>(ordem.Length - itensDaPiorUnidade.Count);
        var bloco = new List<int>(itensDaPiorUnidade.Count);
        var primeiraPosicaoNoRestante = -1;

        foreach (var item in ordem)
        {
            if (alvo.Contains(item))
            {
                bloco.Add(item);
                if (primeiraPosicaoNoRestante < 0) primeiraPosicaoNoRestante = restante.Count;
            }
            else
            {
                restante.Add(item);
            }
        }

        if (bloco.Count == 0)
            return ordem;

        var destino = aleatorio.Next(primeiraPosicaoNoRestante + 1); // 0..primeiraPosição, "mais cedo".

        var resultado = new int[ordem.Length];
        restante.CopyTo(0, resultado, 0, destino);
        bloco.CopyTo(resultado, destino);
        restante.CopyTo(destino, resultado, destino + bloco.Count, restante.Count - destino);
        return resultado;
    }
}
