using Microsoft.EntityFrameworkCore;
using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

public sealed class RegistroDeImpressaoRepository(OptimizeDbContext db) : IRegistroDeImpressaoRepository
{
    public async Task<int> SalvarLoteAsync(IReadOnlyList<RegistroDeImpressao> registros, CancellationToken ct = default)
    {
        if (registros.Count == 0) return 0;

        var idsDeMaquina = registros.Select(r => r.MaquinaId).Distinct().ToList();
        var maquinasExistentes = await db.Maquinas.AsNoTracking()
            .Where(m => idsDeMaquina.Contains(m.Id)).Select(m => m.Id).ToHashSetAsync(ct);

        var validos = registros.Where(r => maquinasExistentes.Contains(r.MaquinaId)).ToList();
        if (validos.Count == 0) return 0;

        var idsDoLote = validos.Select(r => r.Id).ToList();
        var existentes = await db.RegistrosDeImpressao
            .Where(r => idsDoLote.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);

        var agora = DateTime.UtcNow;
        var gravados = 0;

        foreach (var novo in validos)
        {
            novo.AtualizadoEm = agora;

            if (!existentes.TryGetValue(novo.Id, out var existente))
            {
                db.RegistrosDeImpressao.Add(novo);
                gravados++;
                continue;
            }

            // Duas proteções pra tinta já gravada (porte exato de db/records.js):
            // 1. Uma releitura do CSV calcula tinta por área — depois que o contador CMYK real
            //    foi anexado ao registro pelo monitor ao vivo, essa estimativa nunca apaga o
            //    valor exato salvo.
            // 2. A importação de histórico vem sem a divisão por canal (cara demais pela rede)
            //    — ela não pode zerar os canais que o monitor ao vivo já gravou.
            var mantemTinta =
                (existente.TipoDeOrigem == "csv" && !existente.TintaExperimental && existente.CanaisDeTinta is not null && novo.TintaExperimental) ||
                (novo.CanaisDeTinta is null && existente.CanaisDeTinta is not null);

            var tintaMl = mantemTinta ? existente.TintaMl : novo.TintaMl;
            var tintaExperimental = mantemTinta ? existente.TintaExperimental : novo.TintaExperimental;
            var canaisDeTinta = mantemTinta ? existente.CanaisDeTinta : novo.CanaisDeTinta;

            db.Entry(existente).CurrentValues.SetValues(novo);
            existente.TintaMl = tintaMl;
            existente.TintaExperimental = tintaExperimental;
            existente.CanaisDeTinta = canaisDeTinta;
            gravados++;
        }

        await db.SaveChangesAsync(ct);
        return gravados;
    }

    public async Task<List<RegistroDeImpressao>> ListarIntervaloAsync(string? maquinaId, string dataInicioIso, string dataFimIso, CancellationToken ct = default)
    {
        var query = db.RegistrosDeImpressao.AsNoTracking()
            .Where(r => r.Data.CompareTo(dataInicioIso) >= 0 && r.Data.CompareTo(dataFimIso) <= 0);

        if (!string.IsNullOrEmpty(maquinaId) && maquinaId != "all")
            query = query.Where(r => r.MaquinaId == maquinaId);

        return await query.OrderByDescending(r => r.DataHora).ToListAsync(ct);
    }

    public async Task<List<RegistroDeImpressao>> ListarTodosAsync(CancellationToken ct = default) =>
        await db.RegistrosDeImpressao.AsNoTracking().OrderByDescending(r => r.DataHora).ToListAsync(ct);
}
