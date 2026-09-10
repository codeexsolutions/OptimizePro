namespace OptimizePro.Painel;

public sealed class UsuarioService(IUsuarioRepository repositorio) : IUsuarioService
{
    public async Task<Usuario> CadastrarAsync(string login, string nome, string senha, IReadOnlyList<ModuloDoPainel> modulosLiberados, bool ehAdministrador, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(login)) throw new ArgumentException("Informe o login.", nameof(login));
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Informe o nome.", nameof(nome));
        if (string.IsNullOrWhiteSpace(senha)) throw new ArgumentException("Informe a senha.", nameof(senha));

        var loginNormalizado = login.Trim();
        if (await repositorio.ObterPorLoginAsync(loginNormalizado, ct) is not null)
            throw new LoginJaExisteException(loginNormalizado);

        var (hash, sal) = HashDeSenha.Gerar(senha);

        var usuario = new Usuario
        {
            Id = "",
            Login = loginNormalizado,
            Nome = nome.Trim(),
            SenhaHash = hash,
            SenhaSal = sal,
            EhAdministrador = ehAdministrador,
            ModulosLiberados = modulosLiberados.ToList(),
        };

        return await repositorio.CriarAsync(usuario, ct);
    }

    public Task<List<Usuario>> ListarAsync(CancellationToken ct = default) => repositorio.ListarAsync(ct);

    public Task<Usuario?> ObterAsync(string id, CancellationToken ct = default) => repositorio.ObterAsync(id, ct);

    public Task<int> ContarHabilitadosAsync(CancellationToken ct = default) => repositorio.ContarHabilitadosAsync(ct);

    public Task<bool> AtualizarAsync(string id, string nome, IReadOnlyList<ModuloDoPainel> modulosLiberados, bool ehAdministrador, CancellationToken ct = default) =>
        repositorio.AtualizarAsync(id, nome, modulosLiberados, ehAdministrador, ct);

    public Task<bool> RedefinirSenhaAsync(string id, string novaSenha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(novaSenha)) throw new ArgumentException("Informe a nova senha.", nameof(novaSenha));
        var (hash, sal) = HashDeSenha.Gerar(novaSenha);
        return repositorio.AtualizarSenhaAsync(id, hash, sal, ct);
    }

    public Task<bool> AtualizarHabilitadoAsync(string id, bool habilitado, CancellationToken ct = default) =>
        repositorio.AtualizarHabilitadoAsync(id, habilitado, ct);

    public Task<bool> ExcluirAsync(string id, CancellationToken ct = default) => repositorio.ExcluirAsync(id, ct);
}
