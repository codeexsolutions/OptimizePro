namespace OptimizePro.Central;

public sealed class PainelDeStaffService(IInstalacaoRepository instalacoes, IUsuarioAdminService usuarios) : IPainelDeStaffService
{
    public async Task<List<InstalacaoParaStaffDto>> ListarInstalacoesAsync(CancellationToken ct = default)
    {
        var todas = await instalacoes.ListarTodasAsync(ct);
        var resultado = new List<InstalacaoParaStaffDto>(todas.Count);

        foreach (var instalacao in todas)
        {
            var usuariosDaInstalacao = await usuarios.ListarAsync(instalacao.Id, ct);
            var administradores = usuariosDaInstalacao
                .Where(u => u.EhAdministrador)
                .Select(u => new AdministradorDaInstalacaoDto(u.Id, u.Login, u.Nome, u.Habilitado))
                .ToList();

            resultado.Add(new InstalacaoParaStaffDto(
                instalacao.Id, instalacao.Codigo, instalacao.NomeDaFabrica,
                instalacao.CriadoEm, instalacao.UltimaSincronizacaoEm, administradores));
        }

        return resultado;
    }
}
