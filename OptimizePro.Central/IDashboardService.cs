namespace OptimizePro.Central;

public sealed record SemanaDeReposicaoDto(string InicioDaSemana, string FimDaSemana, double MetragemTotal, int Quantidade, List<RegistroDeImpressaoDto> Itens);
public sealed record RespostaDeReposicaoDto(List<SemanaDeReposicaoDto> Semanas, double MetragemTotal, int QuantidadeTotal);
public sealed record ImpressoraResumoDto(string MaquinaId, string Nome, string Tipo, bool Habilitada, int TrabalhosHoje, double MetragemHoje, string? UltimoTrabalho, string? UltimoHorario);

/// <summary>Fachada de leitura pro painel remoto (§24.6) — lê só o espelho JSON já sincronizado (nunca fala com o PC da fábrica direto).</summary>
public interface IDashboardService
{
    Task<List<MaquinaDto>> ObterMaquinasAsync(string instalacaoId, CancellationToken ct = default);
    Task<List<ImpressoraResumoDto>> ObterImpressorasAsync(string instalacaoId, CancellationToken ct = default);
    Task<List<RegistroDeImpressaoDto>> ObterHistoricoAsync(string instalacaoId, CancellationToken ct = default);
    Task<RespostaDeReposicaoDto> ObterReposicaoAsync(string instalacaoId, CancellationToken ct = default);
    Task<List<PedidoDto>> ObterPedidosAsync(string instalacaoId, CancellationToken ct = default);
    Task<List<OrdemDeServicoDto>> ObterOrdensDeServicoAsync(string instalacaoId, CancellationToken ct = default);
}
