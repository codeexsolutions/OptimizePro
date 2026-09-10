using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

/// <summary>Espelha <c>impressoras/db/machines.js</c> (§22) — cadastro da frota de impressoras.</summary>
public interface IMaquinaRepository
{
    Task<List<Maquina>> ListarAsync(bool incluirDesabilitadas, CancellationToken ct = default);
    Task<Maquina?> ObterAsync(string id, CancellationToken ct = default);
    Task<Maquina?> ObterPorHostAsync(string host, CancellationToken ct = default);

    /// <summary>Insere ou atualiza por <see cref="Maquina.Id"/> — o mesmo "upsertMachine" da referência.</summary>
    Task<Maquina> SalvarAsync(Maquina maquina, CancellationToken ct = default);

    Task<bool> AtualizarHabilitadaAsync(string id, bool habilitada, CancellationToken ct = default);

    /// <summary>Exclusão definitiva — cadeia (registros/pedido-itens) já cai em cascata pelo FK (§3 do schema).</summary>
    Task<bool> ExcluirAsync(string id, CancellationToken ct = default);
}
