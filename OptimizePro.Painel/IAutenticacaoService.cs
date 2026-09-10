namespace OptimizePro.Painel;

public sealed record ResultadoDoLogin(bool Sucesso, Usuario? Usuario, string? Erro);

/// <summary>Confere login+senha do painel (§23.1) — quem chama isso decide o que fazer depois (emitir cookie/token; isso entra na fase de API/React).</summary>
public interface IAutenticacaoService
{
    Task<ResultadoDoLogin> AutenticarAsync(string login, string senha, CancellationToken ct = default);
}
