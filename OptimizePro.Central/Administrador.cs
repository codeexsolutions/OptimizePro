namespace OptimizePro.Central;

/// <summary>
/// Login da Codeex Solutions (dono do produto), não de um cliente — vê TODAS as instalações,
/// não só uma (§26). Completamente separado do <see cref="Usuario"/> sincronizado de cada
/// instalação: aquele é escopado por <c>InstalacaoId</c> via JWT; este não tem instalação
/// nenhuma, é "vê tudo".
/// </summary>
public class Administrador
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public required string Nome { get; set; }
    public required byte[] SenhaHash { get; set; }
    public required byte[] SenhaSal { get; set; }
    public DateTime CriadoEm { get; set; }
}
