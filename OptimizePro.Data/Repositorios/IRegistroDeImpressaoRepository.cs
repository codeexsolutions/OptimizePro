using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

public interface IRegistroDeImpressaoRepository
{
    /// <summary>
    /// Upsert em lote com a mesma proteção de tinta de <c>db/records.js</c>: uma releitura do
    /// histórico (estimativa por área) nunca apaga um contador de tinta exato que o monitor ao
    /// vivo já gravou. Só grava registro de máquina que ainda existe (§ mesmo motivo da
    /// referência — evita ressuscitar histórico de máquina excluída).
    /// </summary>
    Task<int> SalvarLoteAsync(IReadOnlyList<RegistroDeImpressao> registros, CancellationToken ct = default);

    /// <summary>Porte de <c>db/records.js#queryRange</c> — <paramref name="maquinaId"/> nulo ou "all" traz todas as máquinas.</summary>
    Task<List<RegistroDeImpressao>> ListarIntervaloAsync(string? maquinaId, string dataInicioIso, string dataFimIso, CancellationToken ct = default);

    /// <summary>Porte de <c>db/records.js#queryAll</c> — histórico inteiro, sem recorte de data (usado pela Reposição, §22.7).</summary>
    Task<List<RegistroDeImpressao>> ListarTodosAsync(CancellationToken ct = default);
}
