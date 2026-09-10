using Microsoft.EntityFrameworkCore;

namespace OptimizePro.Painel;

public sealed class UsuarioRepository(PainelDbContext db) : IUsuarioRepository
{
    public async Task<Usuario> CriarAsync(Usuario usuario, CancellationToken ct = default)
    {
        usuario.Id = Guid.NewGuid().ToString();
        usuario.CriadoEm = DateTime.UtcNow;
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);
        return usuario;
    }

    public async Task<List<Usuario>> ListarAsync(CancellationToken ct = default) =>
        await db.Usuarios.AsNoTracking().OrderBy(u => u.Nome).ToListAsync(ct);

    public async Task<Usuario?> ObterAsync(string id, CancellationToken ct = default) =>
        await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<Usuario?> ObterPorLoginAsync(string login, CancellationToken ct = default) =>
        await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Login == login, ct);

    public async Task<int> ContarHabilitadosAsync(CancellationToken ct = default) =>
        await db.Usuarios.AsNoTracking().CountAsync(u => u.Habilitado, ct);

    public async Task<bool> AtualizarAsync(string id, string nome, IReadOnlyList<ModuloDoPainel> modulosLiberados, bool ehAdministrador, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return false;

        usuario.Nome = nome;
        usuario.ModulosLiberados = modulosLiberados.ToList();
        usuario.EhAdministrador = ehAdministrador;
        usuario.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AtualizarSenhaAsync(string id, byte[] senhaHash, byte[] senhaSal, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return false;

        usuario.SenhaHash = senhaHash;
        usuario.SenhaSal = senhaSal;
        usuario.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AtualizarHabilitadoAsync(string id, bool habilitado, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return false;

        usuario.Habilitado = habilitado;
        usuario.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task RegistrarLoginAsync(string id, DateTime quando, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return;

        usuario.UltimoLoginEm = quando;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExcluirAsync(string id, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return false;

        db.Usuarios.Remove(usuario);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
