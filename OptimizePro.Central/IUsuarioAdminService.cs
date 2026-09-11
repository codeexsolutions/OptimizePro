namespace OptimizePro.Central;

/// <summary>
/// Gestão de usuários do painel remoto, direto na Central (§24.7). A partir daqui a Central é
/// a fonte de verdade de <see cref="TipoDeDadoSincronizado.Usuario"/> — o app desktop nunca
/// teve tela de cadastro de usuário própria (só existia a tabela local, sem UI), então não há
/// dado local relevante a proteger: parar de empurrar "usuario" no push (§24.2) e passar a
/// escrever aqui direto resolve a tensão de "quem manda" sem precisar de sincronização
/// reversa. As outras 5 entidades (maquina/histórico/pedido/OS/faturamento) continuam só de
/// leitura aqui, escritas exclusivamente pelo app desktop.
/// </summary>
public interface IUsuarioAdminService
{
    Task<List<UsuarioSincronizadoDto>> ListarAsync(string instalacaoId, CancellationToken ct = default);

    Task<UsuarioSincronizadoDto> CadastrarAsync(string instalacaoId, string login, string nome, string senha, IReadOnlyList<string> modulosLiberados, bool ehAdministrador, CancellationToken ct = default);

    /// <summary>
    /// Cria o primeiro administrador de uma instalação sem exigir token (§24.7) — só funciona
    /// enquanto <see cref="ListarAsync"/> estiver vazia; depois do primeiro usuário criado essa
    /// janela fecha pra sempre (<see cref="JaTemUsuarioException"/>). É o único jeito de sair do
    /// zero: a Central virou fonte de verdade de Usuario, então precisa de uma porta de entrada
    /// que não dependa de já existir um usuário logado.
    /// </summary>
    Task<UsuarioSincronizadoDto> BootstrapAsync(string instalacaoId, string login, string nome, string senha, CancellationToken ct = default);

    Task<bool> AtualizarAsync(string instalacaoId, string id, string nome, IReadOnlyList<string> modulosLiberados, bool ehAdministrador, CancellationToken ct = default);

    Task<bool> RedefinirSenhaAsync(string instalacaoId, string id, string novaSenha, CancellationToken ct = default);

    Task<bool> AtualizarHabilitadoAsync(string instalacaoId, string id, bool habilitado, CancellationToken ct = default);

    Task<bool> ExcluirAsync(string instalacaoId, string id, CancellationToken ct = default);
}

public sealed class LoginJaExisteException(string login) : Exception($"Já existe um usuário com o login \"{login}\".");

public sealed class JaTemUsuarioException() : Exception("Esta instalação já tem usuários cadastrados — peça pra um administrador te convidar.");
