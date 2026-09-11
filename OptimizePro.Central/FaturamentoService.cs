using System.Text.Json;

namespace OptimizePro.Central;

public sealed class FaturamentoService(IDadoSincronizadoRepository dados, IUsuarioAdminService usuarios) : IFaturamentoService
{
    public async Task<ResumoDeFaturamento?> ObterAsync(string instalacaoId, CancellationToken ct = default)
    {
        var linha = await dados.ObterAsync(instalacaoId, TipoDeDadoSincronizado.Faturamento, "1", ct);
        if (linha is null) return null;

        FaturamentoSincronizadoDto? configuracao;
        try
        {
            configuracao = JsonSerializer.Deserialize<FaturamentoSincronizadoDto>(linha.DadosJson);
        }
        catch (JsonException)
        {
            return null; // linha corrompida/formato antigo — mesma postura de tolerância do DashboardService.
        }
        if (configuracao is null) return null;

        // Contagem de habilitados vem de Usuario (fonte de verdade é a Central desde a §24.7),
        // não do que o desktop sincronizou por último — pode ter mudado no painel remoto depois
        // do último ciclo de sync.
        var habilitados = (await usuarios.ListarAsync(instalacaoId, ct)).Count(u => u.Habilitado);
        var extras = Math.Max(0, habilitados - configuracao.LimiteDeUsuariosNoPlano);
        var mensalidadeTotal = configuracao.ValorBaseMensal + extras * configuracao.ValorPorUsuarioExtra;

        return new ResumoDeFaturamento(
            configuracao.ValorBaseMensal, configuracao.ValorPorUsuarioExtra, configuracao.LimiteDeUsuariosNoPlano,
            habilitados, extras, mensalidadeTotal, configuracao.LicencaValidaAte);
    }
}
