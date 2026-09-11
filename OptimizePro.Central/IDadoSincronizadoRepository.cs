namespace OptimizePro.Central;

public interface IDadoSincronizadoRepository
{
    /// <summary>Upsert de cada item pela chave (instalação, tipo, id) — o app desktop reenvia sempre o estado inteiro do item, então "salvar de novo" é sempre seguro.</summary>
    Task SalvarLoteAsync(string instalacaoId, IReadOnlyList<ItemSincronizado> itens, CancellationToken ct = default);

    Task<List<DadoSincronizado>> ListarPorTipoAsync(string instalacaoId, string tipo, CancellationToken ct = default);

    Task<DadoSincronizado?> ObterAsync(string instalacaoId, string tipo, string entidadeId, CancellationToken ct = default);

    /// <summary>Upsert de um único item — usado pela Central quando ELA é quem escreve o dado (ex.: cadastro de usuário via §24.7), não o app desktop.</summary>
    Task SalvarAsync(string instalacaoId, ItemSincronizado item, CancellationToken ct = default);

    Task<bool> ExcluirAsync(string instalacaoId, string tipo, string entidadeId, CancellationToken ct = default);
}
