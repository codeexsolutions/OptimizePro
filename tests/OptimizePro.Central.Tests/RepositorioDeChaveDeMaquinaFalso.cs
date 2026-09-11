using OptimizePro.Central;

namespace OptimizePro.Central.Tests;

public sealed class RepositorioDeChaveDeMaquinaFalso : IChaveDeMaquinaRepository
{
    private readonly List<ChaveDeMaquina> _chaves = [];

    public Task<ChaveDeMaquina?> ObterAsync(string instalacaoId, string maquinaId, CancellationToken ct = default) =>
        Task.FromResult(_chaves.FirstOrDefault(c => c.InstalacaoId == instalacaoId && c.MaquinaId == maquinaId));

    public Task<List<ChaveDeMaquina>> ListarDaInstalacaoAsync(string instalacaoId, CancellationToken ct = default) =>
        Task.FromResult(_chaves.Where(c => c.InstalacaoId == instalacaoId).ToList());

    public Task CriarAsync(ChaveDeMaquina chave, CancellationToken ct = default)
    {
        chave.CriadoEm = DateTime.UtcNow;
        _chaves.Add(chave);
        return Task.CompletedTask;
    }
}
