namespace OptimizePro.Painel;

public sealed class AutenticacaoService(IUsuarioRepository repositorio) : IAutenticacaoService
{
    public async Task<ResultadoDoLogin> AutenticarAsync(string login, string senha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(senha))
            return new ResultadoDoLogin(false, null, "Informe login e senha.");

        var usuario = await repositorio.ObterPorLoginAsync(login.Trim(), ct);

        // Mesma mensagem genérica pra login inexistente e senha errada — não dar pista de
        // qual dos dois está errado é o comportamento correto de segurança aqui.
        if (usuario is null || !HashDeSenha.Conferir(senha, usuario.SenhaHash, usuario.SenhaSal))
            return new ResultadoDoLogin(false, null, "Login ou senha inválidos.");

        if (!usuario.Habilitado)
            return new ResultadoDoLogin(false, null, "Este usuário está desativado.");

        await repositorio.RegistrarLoginAsync(usuario.Id, DateTime.UtcNow, ct);
        return new ResultadoDoLogin(true, usuario, null);
    }
}
