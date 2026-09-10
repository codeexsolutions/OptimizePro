namespace OptimizePro.Central;

public sealed record UsuarioAutenticado(string InstalacaoId, string UsuarioId, string Login, string Nome, bool EhAdministrador, List<string> ModulosLiberados);

public sealed record ResultadoDoLoginRemoto(bool Sucesso, UsuarioAutenticado? Usuario, string? Erro);

/// <summary>
/// Login do painel remoto (§24.4) — diferente de <see cref="IInstalacaoService.AutenticarAsync"/>
/// (que confere a CHAVE DE API de uma instalação inteira, pra sincronização máquina-a-máquina):
/// isto confere LOGIN+SENHA de uma PESSOA dentro de uma instalação, usando o espelho de
/// <c>Usuario</c> que a fase 4 já sincroniza.
/// </summary>
public interface IAutenticacaoDeUsuarioService
{
    Task<ResultadoDoLoginRemoto> AutenticarAsync(string codigoDaInstalacao, string login, string senha, CancellationToken ct = default);
}
