using Microsoft.EntityFrameworkCore;

namespace OptimizePro.Painel;

public sealed class ConfiguracaoDeFaturamentoRepository(PainelDbContext db) : IConfiguracaoDeFaturamentoRepository
{
    public async Task<ConfiguracaoDeFaturamento> ObterAsync(CancellationToken ct = default) =>
        await db.ConfiguracoesDeFaturamento.AsNoTracking().FirstOrDefaultAsync(c => c.Id == 1, ct)
        ?? new ConfiguracaoDeFaturamento();

    public async Task SalvarAsync(ConfiguracaoDeFaturamento configuracao, CancellationToken ct = default)
    {
        configuracao.Id = 1;
        configuracao.AtualizadoEm = DateTime.UtcNow;

        var existente = await db.ConfiguracoesDeFaturamento.FirstOrDefaultAsync(c => c.Id == 1, ct);
        if (existente is null)
        {
            db.ConfiguracoesDeFaturamento.Add(configuracao);
        }
        else
        {
            existente.ValorBaseMensal = configuracao.ValorBaseMensal;
            existente.ValorPorUsuarioExtra = configuracao.ValorPorUsuarioExtra;
            existente.LimiteDeUsuariosNoPlano = configuracao.LimiteDeUsuariosNoPlano;
            existente.AtualizadoEm = configuracao.AtualizadoEm;
        }

        await db.SaveChangesAsync(ct);
    }
}
