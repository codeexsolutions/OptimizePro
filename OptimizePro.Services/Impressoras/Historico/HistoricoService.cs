using OptimizePro.Data.Repositorios;

namespace OptimizePro.Services.Impressoras.Historico;

public sealed class HistoricoService(IRegistroDeImpressaoRepository registroRepositorio, SincronizadorDeHistoricoService sincronizador) : IHistoricoService
{
    public async Task<RespostaDeHistorico> ObterAsync(string? maquinaId, string dataInicioIso, string dataFimIso, CancellationToken ct = default)
    {
        var registros = await registroRepositorio.ListarIntervaloAsync(maquinaId, dataInicioIso, dataFimIso, ct);
        return new RespostaDeHistorico(registros, ResumoDoHistorico.DeRegistros(registros));
    }

    public Task<int> AtualizarAgoraAsync(string dataInicioIso, string dataFimIso, CancellationToken ct = default) =>
        sincronizador.SincronizarTodasAsync(dataInicioIso, dataFimIso, ct);
}
