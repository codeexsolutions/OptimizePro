using Microsoft.EntityFrameworkCore;

namespace OptimizePro.Central;

public sealed class DadoSincronizadoRepository(CentralDbContext db) : IDadoSincronizadoRepository
{
    public async Task SalvarLoteAsync(string instalacaoId, IReadOnlyList<ItemSincronizado> itens, CancellationToken ct = default)
    {
        if (itens.Count == 0) return;

        var chaves = itens.Select(i => (i.Tipo, i.EntidadeId)).ToHashSet();
        var existentes = await db.DadosSincronizados
            .Where(d => d.InstalacaoId == instalacaoId)
            .ToListAsync(ct); // uma instalação não deve ter volume grande o bastante pra isso doer; revisitar se crescer muito.
        var existentesPorChave = existentes
            .Where(d => chaves.Contains((d.Tipo, d.EntidadeId)))
            .ToDictionary(d => (d.Tipo, d.EntidadeId));

        foreach (var item in itens)
        {
            if (existentesPorChave.TryGetValue((item.Tipo, item.EntidadeId), out var existente))
            {
                existente.DadosJson = item.DadosJson;
                existente.AtualizadoEm = item.AtualizadoEm;
            }
            else
            {
                db.DadosSincronizados.Add(new DadoSincronizado
                {
                    InstalacaoId = instalacaoId,
                    Tipo = item.Tipo,
                    EntidadeId = item.EntidadeId,
                    DadosJson = item.DadosJson,
                    AtualizadoEm = item.AtualizadoEm,
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<List<DadoSincronizado>> ListarPorTipoAsync(string instalacaoId, string tipo, CancellationToken ct = default) =>
        await db.DadosSincronizados.AsNoTracking()
            .Where(d => d.InstalacaoId == instalacaoId && d.Tipo == tipo)
            .ToListAsync(ct);

    public async Task<DadoSincronizado?> ObterAsync(string instalacaoId, string tipo, string entidadeId, CancellationToken ct = default) =>
        await db.DadosSincronizados.AsNoTracking()
            .FirstOrDefaultAsync(d => d.InstalacaoId == instalacaoId && d.Tipo == tipo && d.EntidadeId == entidadeId, ct);

    public async Task SalvarAsync(string instalacaoId, ItemSincronizado item, CancellationToken ct = default)
    {
        var existente = await db.DadosSincronizados
            .FirstOrDefaultAsync(d => d.InstalacaoId == instalacaoId && d.Tipo == item.Tipo && d.EntidadeId == item.EntidadeId, ct);

        if (existente is not null)
        {
            existente.DadosJson = item.DadosJson;
            existente.AtualizadoEm = item.AtualizadoEm;
        }
        else
        {
            db.DadosSincronizados.Add(new DadoSincronizado
            {
                InstalacaoId = instalacaoId,
                Tipo = item.Tipo,
                EntidadeId = item.EntidadeId,
                DadosJson = item.DadosJson,
                AtualizadoEm = item.AtualizadoEm,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExcluirAsync(string instalacaoId, string tipo, string entidadeId, CancellationToken ct = default)
    {
        var existente = await db.DadosSincronizados
            .FirstOrDefaultAsync(d => d.InstalacaoId == instalacaoId && d.Tipo == tipo && d.EntidadeId == entidadeId, ct);
        if (existente is null) return false;

        db.DadosSincronizados.Remove(existente);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
