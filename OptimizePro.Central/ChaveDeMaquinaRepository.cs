using Microsoft.EntityFrameworkCore;

namespace OptimizePro.Central;

public sealed class ChaveDeMaquinaRepository(CentralDbContext db) : IChaveDeMaquinaRepository
{
    public async Task<ChaveDeMaquina?> ObterAsync(string instalacaoId, string maquinaId, CancellationToken ct = default) =>
        await db.ChavesDeMaquina.AsNoTracking()
            .FirstOrDefaultAsync(c => c.InstalacaoId == instalacaoId && c.MaquinaId == maquinaId, ct);

    public async Task<List<ChaveDeMaquina>> ListarDaInstalacaoAsync(string instalacaoId, CancellationToken ct = default) =>
        await db.ChavesDeMaquina.AsNoTracking().Where(c => c.InstalacaoId == instalacaoId).ToListAsync(ct);

    public async Task CriarAsync(ChaveDeMaquina chave, CancellationToken ct = default)
    {
        chave.CriadoEm = DateTime.UtcNow;
        db.ChavesDeMaquina.Add(chave);
        await db.SaveChangesAsync(ct);
    }
}
