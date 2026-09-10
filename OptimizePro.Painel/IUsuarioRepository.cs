namespace OptimizePro.Painel;

public interface IUsuarioRepository
{
    Task<Usuario> CriarAsync(Usuario usuario, CancellationToken ct = default);

    Task<List<Usuario>> ListarAsync(CancellationToken ct = default);

    Task<Usuario?> ObterAsync(string id, CancellationToken ct = default);

    Task<Usuario?> ObterPorLoginAsync(string login, CancellationToken ct = default);

    /// <summary>Quantos usuários habilitados existem — base pro cálculo de usuário extra (§23.1/§23.2, plano padrão até 7).</summary>
    Task<int> ContarHabilitadosAsync(CancellationToken ct = default);

    Task<bool> AtualizarAsync(string id, string nome, IReadOnlyList<ModuloDoPainel> modulosLiberados, bool ehAdministrador, CancellationToken ct = default);

    Task<bool> AtualizarSenhaAsync(string id, byte[] senhaHash, byte[] senhaSal, CancellationToken ct = default);

    Task<bool> AtualizarHabilitadoAsync(string id, bool habilitado, CancellationToken ct = default);

    Task RegistrarLoginAsync(string id, DateTime quando, CancellationToken ct = default);

    Task<bool> ExcluirAsync(string id, CancellationToken ct = default);
}
