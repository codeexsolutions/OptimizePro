using Microsoft.EntityFrameworkCore;
using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

public sealed class ConfiguracaoRepository(OptimizeDbContext db) : IConfiguracaoRepository
{
    public async Task<Dictionary<string, string?>> ObterTodasAsync(CancellationToken ct = default) =>
        await db.ConfiguracoesApp.AsNoTracking().ToDictionaryAsync(c => c.Chave, c => c.Valor, ct);

    public async Task DefinirAsync(string chave, string? valor, CancellationToken ct = default)
    {
        var existente = await db.ConfiguracoesApp.FindAsync([chave], ct);
        if (existente is null)
        {
            db.ConfiguracoesApp.Add(new ConfiguracaoApp { Chave = chave, Valor = valor });
        }
        else
        {
            existente.Valor = valor;
        }

        await db.SaveChangesAsync(ct);
    }
}
