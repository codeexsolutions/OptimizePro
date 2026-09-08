namespace OptimizePro.Data.Entidades;

/// <summary>
/// Tabela <c>projeto_clientes</c> (§3.7). Nome proposital, NÃO renomear pra "Cliente" —
/// instalações antigas têm uma tabela <c>clientes</c> legada (módulo comercial removido)
/// com schema incompatível.
/// </summary>
public class ProjetoCliente
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public string? Observacoes { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public List<Projeto> Projetos { get; set; } = [];
}

/// <summary>Tabela <c>projetos</c> (§3.8).</summary>
public class Projeto
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public ProjetoCliente? Cliente { get; set; }

    public required string Nome { get; set; }
    public string? Observacoes { get; set; }
    public double? LarguraTecido { get; set; }
    public double? Espaco { get; set; }
    public double? Margem { get; set; }

    /// <summary>"180" | "fixa" | "livre" | nulo.</summary>
    public string? Giro { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public List<ProjetoPeca> Pecas { get; set; } = [];
}

/// <summary>Tabela <c>projeto_pecas</c> (§3.9).</summary>
public class ProjetoPeca
{
    public int Id { get; set; }
    public int ProjetoId { get; set; }
    public Projeto? Projeto { get; set; }

    public required string Nome { get; set; }

    /// <summary>Nome físico em <c>uploads/projetos</c>.</summary>
    public required string Arquivo { get; set; }

    public double Largura { get; set; }
    public double Altura { get; set; }
    public int Quantidade { get; set; } = 1;
    public int Ordem { get; set; }

    /// <summary>Data URL ~240px, limite 200.000 caracteres, nullable — só essa coluna é gravada em "patch" de miniatura (§6.2 da arquitetura).</summary>
    public string? Miniatura { get; set; }
}
