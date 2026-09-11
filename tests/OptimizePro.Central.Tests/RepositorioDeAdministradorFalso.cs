using OptimizePro.Central;

namespace OptimizePro.Central.Tests;

public sealed class RepositorioDeAdministradorFalso : IAdministradorRepository
{
    private readonly List<Administrador> _administradores = [];

    public Task<Administrador?> ObterPorEmailAsync(string email, CancellationToken ct = default) =>
        Task.FromResult(_administradores.FirstOrDefault(a => a.Email == email));

    public Task<int> ContarAsync(CancellationToken ct = default) => Task.FromResult(_administradores.Count);

    public Task<Administrador> CriarAsync(Administrador administrador, CancellationToken ct = default)
    {
        _administradores.Add(administrador);
        return Task.FromResult(administrador);
    }
}
