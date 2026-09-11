using Microsoft.EntityFrameworkCore;

namespace OptimizePro.Central;

public sealed class InstalacaoRepository(CentralDbContext db) : IInstalacaoRepository
{
    public async Task<Instalacao?> ObterPorClienteIdHashAsync(uint clienteIdHash, CancellationToken ct = default) =>
        await db.Instalacoes.AsNoTracking().FirstOrDefaultAsync(i => i.ClienteIdHash == clienteIdHash, ct);

    public async Task<Instalacao?> ObterAsync(string id, CancellationToken ct = default) =>
        await db.Instalacoes.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<Instalacao?> ObterPorCodigoAsync(string codigo, CancellationToken ct = default) =>
        await db.Instalacoes.AsNoTracking().FirstOrDefaultAsync(i => i.Codigo == codigo, ct);

    public async Task<List<Instalacao>> ListarTodasAsync(CancellationToken ct = default) =>
        await db.Instalacoes.AsNoTracking().OrderByDescending(i => i.CriadoEm).ToListAsync(ct);

    public async Task<Instalacao> CriarAsync(Instalacao instalacao, CancellationToken ct = default)
    {
        instalacao.Id = Guid.NewGuid().ToString();
        instalacao.CriadoEm = DateTime.UtcNow;
        db.Instalacoes.Add(instalacao);
        await db.SaveChangesAsync(ct);
        return instalacao;
    }

    public async Task RegistrarSincronizacaoAsync(string id, DateTime quando, CancellationToken ct = default)
    {
        var instalacao = await db.Instalacoes.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (instalacao is null) return;

        instalacao.UltimaSincronizacaoEm = quando;
        await db.SaveChangesAsync(ct);
    }
}
