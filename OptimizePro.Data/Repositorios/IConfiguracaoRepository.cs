namespace OptimizePro.Data.Repositorios;

/// <summary>Acesso genérico à tabela chave/valor <c>configuracao_app</c> (§15 da arquitetura).</summary>
public interface IConfiguracaoRepository
{
    Task<Dictionary<string, string?>> ObterTodasAsync(CancellationToken ct = default);

    /// <summary>Upsert por chave.</summary>
    Task DefinirAsync(string chave, string? valor, CancellationToken ct = default);
}
