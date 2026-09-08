using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

/// <summary>Projeção leve de <c>molde_pecas</c> usada só pra agregar tamanhos/contagens sem carregar o grafo inteiro (§6.1 da arquitetura).</summary>
public sealed record ResumoPecaMolde(int MoldeId, string Tamanho, int Quantidade);

/// <summary>Espelha as rotas de <c>moldes-api.js</c> (§4.2 da especificação) — cada método corresponde a uma rota antiga, chamada in-process (§5.4 da arquitetura).</summary>
public interface IMoldeRepository
{
    Task<List<Molde>> ListarAsync(CancellationToken ct = default);
    Task<Molde?> ObterAsync(int id, CancellationToken ct = default);
    Task<bool> ExisteAsync(int id, CancellationToken ct = default);
    Task<int> CriarAsync(Molde molde, CancellationToken ct = default);
    Task<List<ResumoPecaMolde>> ListarResumoDePecasAsync(CancellationToken ct = default);

    /// <summary>Substitui nome/observações e o conjunto inteiro de peças (delete+reinsert, §4.2). Retorna false se o molde não existir.</summary>
    Task<bool> AtualizarAsync(int id, string nome, string? observacoes, List<MoldePeca> pecas, DateTime atualizadoEm, CancellationToken ct = default);

    Task ExcluirAsync(int id, CancellationToken ct = default);

    Task<List<MoldeArte>> ListarArtesAsync(int moldeId, CancellationToken ct = default);

    /// <summary>Upsert de estampa por <paramref name="arteId"/> opcional — substitui as peças por inteiro. Retorna o id da estampa, ou null se <paramref name="arteId"/> informado não pertencer ao molde.</summary>
    Task<int?> UpsertArteAsync(int moldeId, int? arteId, string nome, List<MoldeArtePeca> pecas, DateTime agora, CancellationToken ct = default);

    /// <summary>Retorna false se a estampa não existir (ou não pertencer ao molde).</summary>
    Task<bool> ExcluirArteAsync(int moldeId, int arteId, CancellationToken ct = default);

    /// <summary>Todos os nomes de arquivo de arte referenciados no banco inteiro — usado pela faxina de órfãos (§6, regra crítica: conferir contra a tabela inteira).</summary>
    Task<List<string>> ListarTodosArquivosDeArteAsync(CancellationToken ct = default);
}
