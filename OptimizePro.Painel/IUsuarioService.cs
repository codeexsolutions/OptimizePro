namespace OptimizePro.Painel;

/// <summary>Fachada de cadastro de usuários do painel (§23.1) — cuida do hash de senha e das regras de negócio; a tela nunca toca <see cref="HashDeSenha"/> direto.</summary>
public interface IUsuarioService
{
    /// <summary>Lança <see cref="LoginJaExisteException"/> se o login já estiver em uso.</summary>
    Task<Usuario> CadastrarAsync(string login, string nome, string senha, IReadOnlyList<ModuloDoPainel> modulosLiberados, bool ehAdministrador, CancellationToken ct = default);

    Task<List<Usuario>> ListarAsync(CancellationToken ct = default);

    Task<Usuario?> ObterAsync(string id, CancellationToken ct = default);

    /// <summary>Quantos usuários habilitados existem agora — base do plano padrão de até 7 (§23.2 cuida do valor cobrado acima disso).</summary>
    Task<int> ContarHabilitadosAsync(CancellationToken ct = default);

    Task<bool> AtualizarAsync(string id, string nome, IReadOnlyList<ModuloDoPainel> modulosLiberados, bool ehAdministrador, CancellationToken ct = default);

    Task<bool> RedefinirSenhaAsync(string id, string novaSenha, CancellationToken ct = default);

    Task<bool> AtualizarHabilitadoAsync(string id, bool habilitado, CancellationToken ct = default);

    Task<bool> ExcluirAsync(string id, CancellationToken ct = default);
}

public sealed class LoginJaExisteException(string login) : Exception($"Já existe um usuário com o login \"{login}\".");
