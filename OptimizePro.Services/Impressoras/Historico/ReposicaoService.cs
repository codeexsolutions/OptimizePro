using System.Globalization;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Services.Impressoras.Historico;

public sealed class ReposicaoService(IRegistroDeImpressaoRepository registroRepositorio) : IReposicaoService
{
    public async Task<RespostaDeReposicao> ObterAsync(CancellationToken ct = default)
    {
        var todos = await registroRepositorio.ListarTodosAsync(ct);
        var achados = todos.Where(r => TextoDeImpressoras.Normalizar(r.Tarefa ?? "").Contains("reposic")).ToList();

        var semanasPorInicio = new Dictionary<string, (string Fim, double Metragem, List<ItemDeReposicao> Itens)>();

        foreach (var registro in achados)
        {
            var (inicio, fim) = LimitesDaSemana(registro.Data);
            if (!semanasPorInicio.TryGetValue(inicio, out var semana))
                semana = (fim, 0, []);

            semana.Itens.Add(new ItemDeReposicao(registro.Id, registro.Data, registro.Hora, registro.NomeDaMaquina, registro.Tarefa, registro.ComprimentoDeImpressao));
            semanasPorInicio[inicio] = (fim, semana.Metragem + registro.ComprimentoDeImpressao, semana.Itens);
        }

        var semanas = semanasPorInicio
            .OrderByDescending(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new SemanaDeReposicao(
                kv.Key,
                kv.Value.Fim,
                Math.Round(kv.Value.Metragem, 2),
                kv.Value.Itens.Count,
                kv.Value.Itens
                    .OrderByDescending(i => i.Data, StringComparer.Ordinal)
                    .ThenByDescending(i => i.Hora ?? "", StringComparer.Ordinal)
                    .ToList()))
            .ToList();

        var metragemTotal = Math.Round(achados.Sum(r => r.ComprimentoDeImpressao), 2);
        return new RespostaDeReposicao(semanas, metragemTotal, achados.Count);
    }

    /// <summary>Porte de <c>utils/date.js#weekBounds</c> — semana de segunda a domingo, como a fábrica conta o turno (não a semana do calendário do navegador).</summary>
    private static (string Inicio, string Fim) LimitesDaSemana(string dataIso)
    {
        var data = DateTime.ParseExact(dataIso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var diaDaSemana = (int)data.DayOfWeek; // 0=domingo ... 6=sábado
        var diferencaParaSegunda = diaDaSemana == 0 ? -6 : 1 - diaDaSemana;
        var inicio = data.AddDays(diferencaParaSegunda);
        var fim = inicio.AddDays(6);
        return (inicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), fim.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
