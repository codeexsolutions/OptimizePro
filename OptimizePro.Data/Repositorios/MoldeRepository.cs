using Microsoft.EntityFrameworkCore;
using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

public sealed class MoldeRepository(OptimizeDbContext db) : IMoldeRepository
{
    public async Task<List<Molde>> ListarAsync(CancellationToken ct = default) =>
        await db.Moldes.AsNoTracking().OrderBy(m => m.Id).ToListAsync(ct);

    public async Task<Molde?> ObterAsync(int id, CancellationToken ct = default) =>
        await db.Moldes
            .Include(m => m.Pecas)
            .Include(m => m.Artes).ThenInclude(a => a.Pecas)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<bool> ExisteAsync(int id, CancellationToken ct = default) =>
        await db.Moldes.AsNoTracking().AnyAsync(m => m.Id == id, ct);

    public async Task<int> CriarAsync(Molde molde, CancellationToken ct = default)
    {
        db.Moldes.Add(molde);
        await db.SaveChangesAsync(ct);
        return molde.Id;
    }

    public async Task<List<ResumoPecaMolde>> ListarResumoDePecasAsync(CancellationToken ct = default) =>
        await db.MoldePecas.AsNoTracking()
            .Select(p => new ResumoPecaMolde(p.MoldeId, p.Tamanho, p.Quantidade))
            .ToListAsync(ct);

    public async Task<bool> AtualizarAsync(int id, string nome, string? observacoes, List<MoldePeca> pecas, DateTime atualizadoEm, CancellationToken ct = default)
    {
        var molde = await db.Moldes.Include(m => m.Pecas).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (molde is null) return false;

        molde.Nome = nome;
        molde.Observacoes = observacoes;
        molde.AtualizadoEm = atualizadoEm;

        db.MoldePecas.RemoveRange(molde.Pecas);
        foreach (var peca in pecas) peca.MoldeId = id;
        db.MoldePecas.AddRange(pecas);

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ExcluirAsync(int id, CancellationToken ct = default)
    {
        var molde = await db.Moldes.FindAsync([id], ct);
        if (molde is null) return;

        db.Moldes.Remove(molde);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<MoldeArte>> ListarArtesAsync(int moldeId, CancellationToken ct = default) =>
        await db.MoldeArtes.AsNoTracking()
            .Include(a => a.Pecas)
            .Where(a => a.MoldeId == moldeId)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

    public async Task<int?> UpsertArteAsync(int moldeId, int? arteId, string nome, List<MoldeArtePeca> pecas, DateTime agora, CancellationToken ct = default)
    {
        MoldeArte? arte = null;
        if (arteId is { } id)
        {
            arte = await db.MoldeArtes.Include(a => a.Pecas).FirstOrDefaultAsync(a => a.Id == id && a.MoldeId == moldeId, ct);
            if (arte is null) return null;
        }

        if (arte is null)
        {
            arte = new MoldeArte { MoldeId = moldeId, Nome = nome, CriadoEm = agora, Pecas = pecas };
            db.MoldeArtes.Add(arte);
        }
        else
        {
            arte.Nome = nome;
            arte.AtualizadoEm = agora;
            db.MoldeArtePecas.RemoveRange(arte.Pecas);
            foreach (var peca in pecas) peca.ArteId = arte.Id;
            db.MoldeArtePecas.AddRange(pecas);
        }

        await db.SaveChangesAsync(ct);
        return arte.Id;
    }

    public async Task<bool> ExcluirArteAsync(int moldeId, int arteId, CancellationToken ct = default)
    {
        var arte = await db.MoldeArtes.FirstOrDefaultAsync(a => a.Id == arteId && a.MoldeId == moldeId, ct);
        if (arte is null) return false;

        db.MoldeArtes.Remove(arte);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<string>> ListarTodosArquivosDeArteAsync(CancellationToken ct = default) =>
        await db.MoldeArtePecas.AsNoTracking().Select(p => p.Arquivo).ToListAsync(ct);
}
