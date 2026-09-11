namespace OptimizePro.Central;

public sealed class AdministradorService(IAdministradorRepository administradores) : IAdministradorService
{
    public async Task<AdministradorAutenticado> BootstrapAsync(string email, string nome, string senha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Informe o e-mail.", nameof(email));
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Informe o nome.", nameof(nome));
        if (string.IsNullOrWhiteSpace(senha)) throw new ArgumentException("Informe a senha.", nameof(senha));

        if (await administradores.ContarAsync(ct) > 0)
            throw new JaTemAdministradorException();

        var (hash, sal) = HashDeSenha.Gerar(senha);
        var administrador = new Administrador
        {
            Id = Guid.NewGuid().ToString(),
            Email = email.Trim().ToLowerInvariant(),
            Nome = nome.Trim(),
            SenhaHash = hash,
            SenhaSal = sal,
            CriadoEm = DateTime.UtcNow,
        };

        await administradores.CriarAsync(administrador, ct);
        return new AdministradorAutenticado(administrador.Id, administrador.Email, administrador.Nome);
    }

    public async Task<ResultadoDoLoginDeAdministrador> AutenticarAsync(string email, string senha, CancellationToken ct = default)
    {
        const string erroGenerico = "E-mail ou senha inválidos.";
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
            return new ResultadoDoLoginDeAdministrador(false, null, erroGenerico);

        var administrador = await administradores.ObterPorEmailAsync(email.Trim().ToLowerInvariant(), ct);
        if (administrador is null || !HashDeSenha.Conferir(senha, administrador.SenhaHash, administrador.SenhaSal))
            return new ResultadoDoLoginDeAdministrador(false, null, erroGenerico);

        return new ResultadoDoLoginDeAdministrador(true, new AdministradorAutenticado(administrador.Id, administrador.Email, administrador.Nome), null);
    }
}
