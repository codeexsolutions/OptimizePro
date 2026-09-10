using OptimizePro.Data.Entidades;

namespace OptimizePro.Services.Impressoras.Historico;

public sealed record RespostaDeHistorico(List<RegistroDeImpressao> Registros, ResumoDoHistorico Resumo);

/// <summary>Fachada da tela de Histórico (§22.5) — só lê o banco local (porte de <c>impressoras-api.js</c>'s <c>/history</c>); quem conversa com as máquinas é o <see cref="SincronizadorDeHistoricoService"/>.</summary>
public interface IHistoricoService
{
    Task<RespostaDeHistorico> ObterAsync(string? maquinaId, string dataInicioIso, string dataFimIso, CancellationToken ct = default);

    /// <summary>Sincroniza todas as máquinas habilitadas e devolve a quantidade de registros importados/atualizados.</summary>
    Task<int> AtualizarAgoraAsync(string dataInicioIso, string dataFimIso, CancellationToken ct = default);
}
