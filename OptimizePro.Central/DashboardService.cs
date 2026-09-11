using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OptimizePro.Central;

public sealed class DashboardService(IDadoSincronizadoRepository dados) : IDashboardService
{
    public async Task<List<MaquinaDto>> ObterMaquinasAsync(string instalacaoId, CancellationToken ct = default) =>
        await ListarEDesserializarAsync<MaquinaDto>(instalacaoId, TipoDeDadoSincronizado.Maquina, ct);

    public async Task<List<RegistroDeImpressaoDto>> ObterHistoricoAsync(string instalacaoId, CancellationToken ct = default) =>
        await ListarEDesserializarAsync<RegistroDeImpressaoDto>(instalacaoId, TipoDeDadoSincronizado.RegistroDeImpressao, ct);

    public async Task<List<PedidoDto>> ObterPedidosAsync(string instalacaoId, CancellationToken ct = default) =>
        await ListarEDesserializarAsync<PedidoDto>(instalacaoId, TipoDeDadoSincronizado.Pedido, ct);

    public async Task<List<OrdemDeServicoDto>> ObterOrdensDeServicoAsync(string instalacaoId, CancellationToken ct = default) =>
        await ListarEDesserializarAsync<OrdemDeServicoDto>(instalacaoId, TipoDeDadoSincronizado.OrdemDeServico, ct);

    public async Task<List<ImpressoraResumoDto>> ObterImpressorasAsync(string instalacaoId, CancellationToken ct = default)
    {
        var maquinas = await ObterMaquinasAsync(instalacaoId, ct);
        var registros = await ObterHistoricoAsync(instalacaoId, ct);

        var hoje = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var registrosPorMaquina = registros.Where(r => r.Data == hoje).GroupBy(r => r.MaquinaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return maquinas.Select(m =>
        {
            var doDia = registrosPorMaquina.GetValueOrDefault(m.Id, []);
            var ultimo = doDia.OrderByDescending(r => r.DataHora).FirstOrDefault();
            return new ImpressoraResumoDto(
                m.Id, m.Nome, m.Tipo, m.Habilitada,
                doDia.Count, doDia.Sum(r => r.ComprimentoDeImpressao),
                ultimo?.Tarefa, ultimo?.DataHora);
        }).ToList();
    }

    public async Task<RespostaDeReposicaoDto> ObterReposicaoAsync(string instalacaoId, CancellationToken ct = default)
    {
        var registros = await ObterHistoricoAsync(instalacaoId, ct);
        var achados = registros.Where(r => NormalizarTexto(r.Tarefa ?? "").Contains("reposic")).ToList();

        var semanasPorInicio = new Dictionary<string, (string Fim, double Metragem, List<RegistroDeImpressaoDto> Itens)>();
        foreach (var registro in achados)
        {
            var (inicio, fim) = LimitesDaSemana(registro.Data);
            if (!semanasPorInicio.TryGetValue(inicio, out var semana)) semana = (fim, 0, []);
            semana.Itens.Add(registro);
            semanasPorInicio[inicio] = (fim, semana.Metragem + registro.ComprimentoDeImpressao, semana.Itens);
        }

        var semanas = semanasPorInicio
            .OrderByDescending(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new SemanaDeReposicaoDto(
                kv.Key, kv.Value.Fim, Math.Round(kv.Value.Metragem, 2), kv.Value.Itens.Count,
                kv.Value.Itens.OrderByDescending(i => i.DataHora, StringComparer.Ordinal).ToList()))
            .ToList();

        return new RespostaDeReposicaoDto(semanas, Math.Round(achados.Sum(r => r.ComprimentoDeImpressao), 2), achados.Count);
    }

    private async Task<List<T>> ListarEDesserializarAsync<T>(string instalacaoId, string tipo, CancellationToken ct)
    {
        var linhas = await dados.ListarPorTipoAsync(instalacaoId, tipo, ct);
        var resultado = new List<T>();
        foreach (var linha in linhas)
        {
            try
            {
                var item = JsonSerializer.Deserialize<T>(linha.DadosJson);
                if (item is not null) resultado.Add(item);
            }
            catch (JsonException)
            {
                // Linha corrompida/formato antigo — ignora essa linha, não derruba o dashboard inteiro.
            }
        }
        return resultado;
    }

    /// <summary>Mesmo porte de <c>OptimizePro.Services.Impressoras.TextoDeImpressoras.Normalizar</c> — duplicado de propósito (isolamento).</summary>
    private static string NormalizarTexto(string valor)
    {
        var normalizado = valor.Normalize(NormalizationForm.FormD);
        var semAcento = new StringBuilder();
        foreach (var c in normalizado)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) semAcento.Append(c);
        return semAcento.ToString().ToLowerInvariant();
    }

    /// <summary>Mesmo porte de <c>OptimizePro.Services.Impressoras.Historico.ReposicaoService</c> (semana de segunda a domingo) — duplicado de propósito.</summary>
    private static (string Inicio, string Fim) LimitesDaSemana(string dataIso)
    {
        var data = DateTime.ParseExact(dataIso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var diaDaSemana = (int)data.DayOfWeek;
        var diferencaParaSegunda = diaDaSemana == 0 ? -6 : 1 - diaDaSemana;
        var inicio = data.AddDays(diferencaParaSegunda);
        var fim = inicio.AddDays(6);
        return (inicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), fim.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
