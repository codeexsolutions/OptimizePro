namespace OptimizePro.Central;

public sealed record AdministradorAutenticado(string Id, string Email, string Nome);
public sealed record ResultadoDoLoginDeAdministrador(bool Sucesso, AdministradorAutenticado? Administrador, string? Erro);

public interface IAdministradorService
{
    /// <summary>Só funciona enquanto não existir NENHUM administrador ainda — mesmo padrão de <see cref="IUsuarioAdminService.BootstrapAsync"/> (§24.7), aqui pro dono do produto (Codeex Solutions) em vez de um cliente.</summary>
    Task<AdministradorAutenticado> BootstrapAsync(string email, string nome, string senha, CancellationToken ct = default);

    Task<ResultadoDoLoginDeAdministrador> AutenticarAsync(string email, string senha, CancellationToken ct = default);
}

public sealed class JaTemAdministradorException() : Exception("Já existe um administrador cadastrado.");
