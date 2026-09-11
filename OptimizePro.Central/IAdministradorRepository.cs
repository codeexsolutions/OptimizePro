namespace OptimizePro.Central;

public interface IAdministradorRepository
{
    Task<Administrador?> ObterPorEmailAsync(string email, CancellationToken ct = default);

    Task<int> ContarAsync(CancellationToken ct = default);

    Task<Administrador> CriarAsync(Administrador administrador, CancellationToken ct = default);
}
