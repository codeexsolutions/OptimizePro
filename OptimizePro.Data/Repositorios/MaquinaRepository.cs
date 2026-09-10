using Microsoft.EntityFrameworkCore;
using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

public sealed class MaquinaRepository(OptimizeDbContext db) : IMaquinaRepository
{
    public async Task<List<Maquina>> ListarAsync(bool incluirDesabilitadas, CancellationToken ct = default)
    {
        var query = db.Maquinas.AsNoTracking().AsQueryable();
        if (!incluirDesabilitadas) query = query.Where(m => m.Habilitada);
        return await query.OrderBy(m => m.Posicao).ThenBy(m => m.Id).ToListAsync(ct);
    }

    public async Task<Maquina?> ObterAsync(string id, CancellationToken ct = default) =>
        await db.Maquinas.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<Maquina?> ObterPorHostAsync(string host, CancellationToken ct = default) =>
        await db.Maquinas.AsNoTracking().FirstOrDefaultAsync(m => m.Host == host, ct);

    public async Task<Maquina> SalvarAsync(Maquina maquina, CancellationToken ct = default)
    {
        var existente = await db.Maquinas.FirstOrDefaultAsync(m => m.Id == maquina.Id, ct);
        if (existente is null)
        {
            db.Maquinas.Add(maquina);
        }
        else
        {
            // COALESCE do descoberta_em original (§ machines.js) — a primeira detecção não
            // deve ser reescrita a cada nova varredura que reencontra a mesma máquina.
            var descobertaOriginal = existente.DescobertaEm;
            db.Entry(existente).CurrentValues.SetValues(maquina);
            existente.DescobertaEm = descobertaOriginal ?? maquina.DescobertaEm;
        }

        await db.SaveChangesAsync(ct);
        return existente ?? maquina;
    }

    public async Task<bool> AtualizarHabilitadaAsync(string id, bool habilitada, CancellationToken ct = default)
    {
        var maquina = await db.Maquinas.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (maquina is null) return false;

        maquina.Habilitada = habilitada;
        maquina.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ExcluirAsync(string id, CancellationToken ct = default)
    {
        var maquina = await db.Maquinas.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (maquina is null) return false;

        db.Maquinas.Remove(maquina);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
