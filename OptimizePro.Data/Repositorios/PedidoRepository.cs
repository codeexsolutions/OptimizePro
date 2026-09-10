using Microsoft.EntityFrameworkCore;
using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

public sealed class PedidoRepository(OptimizeDbContext db) : IPedidoRepository
{
    public async Task<string> CriarAsync(IReadOnlyList<PedidoItem> itens, string? observacao, CancellationToken ct = default)
    {
        var pedido = new Pedido
        {
            Id = Guid.NewGuid().ToString(),
            CriadoEm = DateTime.UtcNow,
            Status = "aberto",
            Observacao = observacao,
        };

        for (var i = 0; i < itens.Count; i++)
        {
            itens[i].Id = Guid.NewGuid().ToString();
            itens[i].PedidoId = pedido.Id;
            itens[i].Posicao = i;
            itens[i].StatusNaCalandra = "pendente";
        }
        pedido.Itens = itens.ToList();

        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync(ct);
        return pedido.Id;
    }

    public async Task<List<ResumoDoPedido>> ListarAsync(CancellationToken ct = default) =>
        await db.Pedidos.AsNoTracking()
            .OrderByDescending(p => p.CriadoEm)
            .Select(p => new ResumoDoPedido(
                p.Id,
                p.CriadoEm,
                p.Status,
                p.Observacao,
                p.Itens.Count,
                p.Itens.Count(i => i.StatusNaCalandra == "ok"),
                p.Itens.Count(i => i.StatusNaCalandra == "erro")))
            .ToListAsync(ct);

    public async Task<Pedido?> ObterAsync(string id, CancellationToken ct = default) =>
        await db.Pedidos.AsNoTracking()
            .Include(p => p.Itens.OrderBy(i => i.Posicao))
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<bool> ExcluirAsync(string id, CancellationToken ct = default)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pedido is null) return false;

        db.Pedidos.Remove(pedido);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AtualizarStatusAsync(string id, string status, CancellationToken ct = default)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pedido is null) return false;

        pedido.Status = status;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PedidoItem?> AtualizarResultadoDoItemAsync(string pedidoId, string itemId, string status, string? motivo, string? motivoPersonalizado, CancellationToken ct = default)
    {
        var item = await db.PedidoItens.FirstOrDefaultAsync(i => i.Id == itemId && i.PedidoId == pedidoId, ct);
        if (item is null) return null;

        item.StatusNaCalandra = status;
        item.MotivoNaCalandra = motivo;
        item.MotivoPersonalizadoNaCalandra = motivoPersonalizado;
        item.DataNaCalandra = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<PedidoItem?> ObterPorRegistroIdAsync(string registroId, CancellationToken ct = default) =>
        await db.PedidoItens.AsNoTracking()
            .Where(i => i.RegistroId == registroId)
            .OrderByDescending(i => i.Id)
            .FirstOrDefaultAsync(ct);
}
