using OptimizePro.Central;

namespace OptimizePro.Central.Tests;

/// <summary>Fake em memória — sem Postgres real disponível neste ambiente, os testes de <c>InstalacaoService</c> cobrem só a lógica de negócio, não o EF Core/Npgsql em si (isso é só configuração de mapeamento, já conferido pela migração gerada).</summary>
public sealed class RepositorioDeInstalacaoFalso : IInstalacaoRepository
{
    private readonly Dictionary<string, Instalacao> _porId = [];

    public Task<Instalacao?> ObterPorClienteIdHashAsync(uint clienteIdHash, CancellationToken ct = default) =>
        Task.FromResult(_porId.Values.FirstOrDefault(i => i.ClienteIdHash == clienteIdHash));

    public Task<Instalacao?> ObterAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_porId.GetValueOrDefault(id));

    public Task<Instalacao?> ObterPorCodigoAsync(string codigo, CancellationToken ct = default) =>
        Task.FromResult(_porId.Values.FirstOrDefault(i => i.Codigo == codigo));

    public Task<List<Instalacao>> ListarTodasAsync(CancellationToken ct = default) =>
        Task.FromResult(_porId.Values.OrderByDescending(i => i.CriadoEm).ToList());

    public Task<Instalacao> CriarAsync(Instalacao instalacao, CancellationToken ct = default)
    {
        instalacao.Id = Guid.NewGuid().ToString();
        instalacao.CriadoEm = DateTime.UtcNow;
        _porId[instalacao.Id] = instalacao;
        return Task.FromResult(instalacao);
    }

    public Task RegistrarSincronizacaoAsync(string id, DateTime quando, CancellationToken ct = default)
    {
        if (_porId.TryGetValue(id, out var instalacao)) instalacao.UltimaSincronizacaoEm = quando;
        return Task.CompletedTask;
    }
}
