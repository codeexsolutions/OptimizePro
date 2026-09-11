namespace OptimizePro.Central;

/// <summary>Um administrador de uma instalação, visto de fora — nunca inclui hash/sal de senha (só quem loga localmente/no painel da própria instalação precisa disso).</summary>
public sealed record AdministradorDaInstalacaoDto(string Id, string Login, string Nome, bool Habilitado);

public sealed record InstalacaoParaStaffDto(
    string Id, string Codigo, string? NomeDaFabrica, DateTime CriadoEm, DateTime? UltimaSincronizacaoEm,
    List<AdministradorDaInstalacaoDto> Administradores);

public interface IPainelDeStaffService
{
    /// <summary>Todas as instalações + os administradores de cada uma (§26) — visão da Codeex Solutions, cruza tenant, só pra staff autenticado como tal (nunca pro painel de um cliente).</summary>
    Task<List<InstalacaoParaStaffDto>> ListarInstalacoesAsync(CancellationToken ct = default);
}
