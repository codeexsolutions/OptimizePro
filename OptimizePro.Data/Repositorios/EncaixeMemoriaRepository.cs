using Microsoft.EntityFrameworkCore;
using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

public sealed class EncaixeMemoriaRepository(OptimizeDbContext db) : IEncaixeMemoriaRepository
{
    public async Task<EncaixeReceita?> ObterReceitaAsync(string assinatura, string receita, CancellationToken ct = default) =>
        await db.EncaixeReceitas.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Assinatura == assinatura && r.Receita == receita, ct);

    public async Task RegistrarUsoDeReceitaAsync(string assinatura, string receita, bool venceu, CancellationToken ct = default)
    {
        var existente = await db.EncaixeReceitas
            .FirstOrDefaultAsync(r => r.Assinatura == assinatura && r.Receita == receita, ct);

        if (existente is null)
        {
            db.EncaixeReceitas.Add(new EncaixeReceita
            {
                Assinatura = assinatura,
                Receita = receita,
                Usos = 1,
                Vitorias = venceu ? 1 : 0,
                AtualizadoEm = DateTime.UtcNow,
            });
        }
        else
        {
            existente.Usos++;
            if (venceu) existente.Vitorias++;
            existente.AtualizadoEm = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<EncaixeGuardado?> ObterGuardadoAsync(string chave, CancellationToken ct = default) =>
        await db.EncaixeGuardados.AsNoTracking().FirstOrDefaultAsync(g => g.Chave == chave, ct);

    public async Task SalvarGuardadoAsync(EncaixeGuardado guardado, CancellationToken ct = default)
    {
        var existente = await db.EncaixeGuardados.FindAsync([guardado.Chave], ct);

        if (existente is null)
        {
            db.EncaixeGuardados.Add(guardado);
        }
        else
        {
            db.Entry(existente).CurrentValues.SetValues(guardado);
            existente.AtualizadoEm = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RegistrarHistoricoAsync(EncaixeHistorico historico, CancellationToken ct = default)
    {
        db.EncaixeHistoricos.Add(historico);
        await db.SaveChangesAsync(ct);
    }

    public async Task LimparAsync(CancellationToken ct = default)
    {
        await db.EncaixeReceitas.ExecuteDeleteAsync(ct);
        await db.EncaixeGuardados.ExecuteDeleteAsync(ct);
        await db.EncaixeHistoricos.ExecuteDeleteAsync(ct);
        await db.EncaixeRedePesos.ExecuteDeleteAsync(ct);
    }

    public async Task<Dictionary<string, (int Usos, int Vitorias)>> SomarUsosEVitoriasPorReceitaAsync(CancellationToken ct = default)
    {
        var linhas = await db.EncaixeReceitas.AsNoTracking()
            .GroupBy(r => r.Receita)
            .Select(g => new { Receita = g.Key, Usos = g.Sum(r => r.Usos), Vitorias = g.Sum(r => r.Vitorias) })
            .ToListAsync(ct);

        return linhas.ToDictionary(x => x.Receita, x => (x.Usos, x.Vitorias));
    }

    public async Task<List<EncaixeReceita>> ListarReceitasPorAssinaturaAsync(string assinatura, CancellationToken ct = default) =>
        await db.EncaixeReceitas.AsNoTracking().Where(r => r.Assinatura == assinatura).ToListAsync(ct);

    public Task<int> ContarHistoricoTotalAsync(CancellationToken ct = default) =>
        db.EncaixeHistoricos.AsNoTracking().CountAsync(ct);

    public Task<int> ContarHistoricoPorAssinaturaAsync(string assinatura, CancellationToken ct = default) =>
        db.EncaixeHistoricos.AsNoTracking().CountAsync(h => h.Assinatura == assinatura, ct);

    public async Task<double?> MenorConsumoPorAssinaturaAsync(string assinatura, CancellationToken ct = default) =>
        await db.EncaixeHistoricos.AsNoTracking()
            .Where(h => h.Assinatura == assinatura && h.Consumo != null)
            .Select(h => h.Consumo)
            .OrderBy(c => c)
            .FirstOrDefaultAsync(ct);

    public Task<EncaixeRedePesos?> ObterRedePesosAsync(CancellationToken ct = default) =>
        db.EncaixeRedePesos.AsNoTracking().FirstOrDefaultAsync(r => r.Id == 1, ct);

    public async Task SalvarRedePesosAsync(EncaixeRedePesos pesos, CancellationToken ct = default)
    {
        var existente = await db.EncaixeRedePesos.FindAsync([1], ct);

        if (existente is null)
        {
            db.EncaixeRedePesos.Add(pesos);
        }
        else
        {
            db.Entry(existente).CurrentValues.SetValues(pesos);
        }

        await db.SaveChangesAsync(ct);
    }

    public Task<List<EncaixeHistorico>> ListarHistoricoParaTreinoAsync(CancellationToken ct = default) =>
        db.EncaixeHistoricos.AsNoTracking()
            .Where(h => h.FeaturesJson != null && h.PlacarJson != null)
            .ToListAsync(ct);

    public Task<int> ContarAssinaturasDistintasParaTreinoAsync(CancellationToken ct = default) =>
        db.EncaixeHistoricos.AsNoTracking()
            .Where(h => h.FeaturesJson != null && h.PlacarJson != null)
            .Select(h => h.Assinatura)
            .Distinct()
            .CountAsync(ct);
}
