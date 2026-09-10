namespace OptimizePro.Painel;

/// <summary>
/// Um usuário do painel do proprietário (§23) — não é o Windows, é um login próprio do
/// Optimize (proprietário + equipe), cada um com os módulos liberados na hora do cadastro.
/// Plano padrão: até 7 usuários habilitados; acima disso entra cobrança por usuário extra
/// (regra de negócio vive em <see cref="Faturamento.CalcularMensalidade"/>, não aqui).
/// </summary>
public class Usuario
{
    public required string Id { get; set; }
    public required string Login { get; set; }
    public required string Nome { get; set; }

    /// <summary>Hash PBKDF2 da senha — nunca a senha em texto puro (§23.1).</summary>
    public required byte[] SenhaHash { get; set; }
    public required byte[] SenhaSal { get; set; }

    /// <summary>Vê Usuários e Faturamento independente de <see cref="ModulosLiberados"/> — normalmente só o(s) proprietário(s).</summary>
    public bool EhAdministrador { get; set; }

    public bool Habilitado { get; set; } = true;

    public List<ModuloDoPainel> ModulosLiberados { get; set; } = [];

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public DateTime? UltimoLoginEm { get; set; }
}
