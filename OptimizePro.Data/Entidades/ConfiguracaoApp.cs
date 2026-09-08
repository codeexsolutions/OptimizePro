namespace OptimizePro.Data.Entidades;

/// <summary>
/// Tabela chave/valor simples pra configurações do usuário (§15 da arquitetura — código do
/// país/DDD padrão, intervalos de disparo, DPI padrão de exportação) — evita ter um arquivo
/// JSON à parte com estado duplicado do SQLite.
/// </summary>
public class ConfiguracaoApp
{
    public required string Chave { get; set; }
    public string? Valor { get; set; }
}
