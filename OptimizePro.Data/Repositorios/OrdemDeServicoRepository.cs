using Microsoft.EntityFrameworkCore;
using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

public sealed class OrdemDeServicoRepository(OptimizeDbContext db) : IOrdemDeServicoRepository
{
    public async Task<string> CriarAsync(OrdemDeServico ordem, CancellationToken ct = default)
    {
        ordem.Id = Guid.NewGuid().ToString();
        ordem.CriadoEm = DateTime.UtcNow;

        for (var i = 0; i < ordem.Imagens.Count; i++)
        {
            ordem.Imagens[i].Id = Guid.NewGuid().ToString();
            ordem.Imagens[i].OrdemId = ordem.Id;
            ordem.Imagens[i].Posicao = i;
        }

        db.OrdensDeServico.Add(ordem);
        await db.SaveChangesAsync(ct);
        return ordem.Id;
    }

    public async Task<List<ResumoDeOrdemDeServico>> ListarAsync(string? busca = null, CancellationToken ct = default)
    {
        var query = db.OrdensDeServico.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            query = query.Where(o => EF.Functions.Like(o.NomeDoCliente, $"%{termo}%") || (o.Tecido != null && EF.Functions.Like(o.Tecido, $"%{termo}%")));
        }

        return await query
            .OrderByDescending(o => o.Data).ThenByDescending(o => o.CriadoEm)
            .Select(o => new ResumoDeOrdemDeServico(o.Id, o.NomeDoCliente, o.Tecido, o.Data, o.Metros, o.Imagens.Count))
            .ToListAsync(ct);
    }

    public async Task<OrdemDeServico?> ObterAsync(string id, CancellationToken ct = default) =>
        await db.OrdensDeServico.AsNoTracking()
            .Include(o => o.Imagens.OrderBy(i => i.Posicao))
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<(byte[] Dados, string TipoMime)?> ObterImagemAsync(string ordemId, string imagemId, CancellationToken ct = default)
    {
        var imagem = await db.OrdensDeServicoImagens.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == imagemId && i.OrdemId == ordemId, ct);
        return imagem is null ? null : (imagem.Dados, imagem.TipoMime);
    }

    public async Task<bool> ExcluirAsync(string id, CancellationToken ct = default)
    {
        var ordem = await db.OrdensDeServico.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (ordem is null) return false;

        db.OrdensDeServico.Remove(ordem);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
