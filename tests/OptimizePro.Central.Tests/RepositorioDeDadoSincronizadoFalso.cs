using OptimizePro.Central;

namespace OptimizePro.Central.Tests;

public sealed class RepositorioDeDadoSincronizadoFalso : IDadoSincronizadoRepository
{
    private readonly List<DadoSincronizado> _linhas = [];

    public Task SalvarLoteAsync(string instalacaoId, IReadOnlyList<ItemSincronizado> itens, CancellationToken ct = default)
    {
        foreach (var item in itens)
        {
            _linhas.RemoveAll(l => l.InstalacaoId == instalacaoId && l.Tipo == item.Tipo && l.EntidadeId == item.EntidadeId);
            _linhas.Add(new DadoSincronizado { InstalacaoId = instalacaoId, Tipo = item.Tipo, EntidadeId = item.EntidadeId, DadosJson = item.DadosJson, AtualizadoEm = item.AtualizadoEm });
        }
        return Task.CompletedTask;
    }

    public Task<List<DadoSincronizado>> ListarPorTipoAsync(string instalacaoId, string tipo, CancellationToken ct = default) =>
        Task.FromResult(_linhas.Where(l => l.InstalacaoId == instalacaoId && l.Tipo == tipo).ToList());
}
