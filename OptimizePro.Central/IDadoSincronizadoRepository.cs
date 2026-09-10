namespace OptimizePro.Central;

public interface IDadoSincronizadoRepository
{
    /// <summary>Upsert de cada item pela chave (instalação, tipo, id) — o app desktop reenvia sempre o estado inteiro do item, então "salvar de novo" é sempre seguro.</summary>
    Task SalvarLoteAsync(string instalacaoId, IReadOnlyList<ItemSincronizado> itens, CancellationToken ct = default);

    Task<List<DadoSincronizado>> ListarPorTipoAsync(string instalacaoId, string tipo, CancellationToken ct = default);
}
