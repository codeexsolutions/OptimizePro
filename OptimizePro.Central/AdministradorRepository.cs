using Microsoft.EntityFrameworkCore;

namespace OptimizePro.Central;

public sealed class AdministradorRepository(CentralDbContext db) : IAdministradorRepository
{
    public async Task<Administrador?> ObterPorEmailAsync(string email, CancellationToken ct = default) =>
        await db.Administradores.AsNoTracking().FirstOrDefaultAsync(a => a.Email == email, ct);

    public Task<int> ContarAsync(CancellationToken ct = default) => db.Administradores.CountAsync(ct);

    public async Task<Administrador> CriarAsync(Administrador administrador, CancellationToken ct = default)
    {
        db.Administradores.Add(administrador);
        await db.SaveChangesAsync(ct);
        return administrador;
    }
}
