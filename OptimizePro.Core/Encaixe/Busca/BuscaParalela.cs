namespace OptimizePro.Core.Encaixe.Busca;

public sealed record ResultadoDaBuscaParalela(ResultadoDaBusca Melhor, IReadOnlyList<ResultadoDaBusca> ResultadosPorFatia);

/// <summary>
/// Porte de §11.9 — cada fatia roda sua própria <see cref="BuscaDeReceitas"/> independente
/// sobre um subconjunto round-robin das receitas, em paralelo. Em .NET isso é
/// <c>Task.Run</c> simples: sem a complexidade de postMessage/memória isolada dos Web
/// Workers originais — cada fatia só lê os dados de entrada (compartilhados, imutáveis
/// aqui) e escreve no próprio resultado, sem seção crítica nenhuma.
/// </summary>
/// <remarks>
/// Não diferencia "pulo" de varredura entre fatias (§11.9 pede varredura exata nas 2
/// primeiras, "pulando de 3" nas demais) — <see cref="EncaixadorPorContorno"/> ainda não
/// implementa essa otimização de performance (documentado lá), então não há hoje o que
/// diferenciar por fatia; só a divisão de receitas e a execução em paralelo em si.
/// </remarks>
public static class BuscaParalela
{
    public static async Task<ResultadoDaBuscaParalela> BuscarMelhorEncaixeAsync(
        IReadOnlyList<Receita> receitas,
        int quantidadeDeItens,
        Func<CriterioDeOrdem, IReadOnlyList<int>> obterOrdemBase,
        Func<Receita, IReadOnlyList<int>, CancellationToken, ResultadoDeTentativa> executar,
        ParametrosDeBusca parametros,
        Func<IRelogioDeBusca> criarRelogio,
        Func<int, Random> criarAleatorioPorFatia,
        bool reservarUltimaFatiaParaNfp = false,
        double? alvoConsumoCm = null,
        CancellationToken cancelamento = default,
        int? numeroDeFatias = null,
        int? tetoDeTentativasParaTestePorFatia = null,
        Func<Receita, double>? pesoDaReceita = null)
    {
        var n = numeroDeFatias ?? ParticionamentoDeFatias.NumeroDeFatias();
        var (fatiasNormais, fatiaNfp) = ParticionamentoDeFatias.ParticionarComReservaDeNfp(receitas, n, reservarUltimaFatiaParaNfp);

        var tarefas = new List<Task<ResultadoDaBusca>>();

        for (var k = 0; k < fatiasNormais.Count; k++)
        {
            if (fatiasNormais[k].Count == 0)
                continue;

            var receitasDaFatia = fatiasNormais[k];
            var indiceDaFatia = k;

            tarefas.Add(Task.Run(() => BuscaDeReceitas.BuscarMelhorEncaixe(
                receitasDaFatia, quantidadeDeItens, obterOrdemBase, executar, parametros,
                criarRelogio(), criarAleatorioPorFatia(indiceDaFatia), alvoConsumoCm,
                cancelamento, tetoDeTentativasParaTestePorFatia, pesoDaReceita), cancelamento));
        }

        if (fatiaNfp.Count > 0)
        {
            tarefas.Add(Task.Run(() => BuscaDeReceitas.BuscarMelhorEncaixe(
                fatiaNfp, quantidadeDeItens, obterOrdemBase, executar, parametros,
                criarRelogio(), criarAleatorioPorFatia(n - 1), alvoConsumoCm,
                cancelamento, tetoDeTentativasParaTestePorFatia, pesoDaReceita), cancelamento));
        }

        if (tarefas.Count == 0)
            throw new ArgumentException("Nenhuma receita para buscar.", nameof(receitas));

        var resultados = await Task.WhenAll(tarefas);

        // Mesma prioridade de PlacarDeReceita.EhMelhor (§11.7) — menos peça de fora antes de
        // olhar consumo, pra combinar corretamente os "melhores" de fatias diferentes.
        var melhor = resultados.OrderBy(r => r.MelhorNaoEncaixados).ThenBy(r => r.MelhorConsumoCm).First();

        return new ResultadoDaBuscaParalela(melhor, resultados);
    }
}
