using Microsoft.EntityFrameworkCore;
using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

public sealed class ProjetoRepository(OptimizeDbContext db) : IProjetoRepository
{
    public async Task<List<ClienteComContagem>> ListarClientesComContagemAsync(CancellationToken ct = default) =>
        await db.ProjetoClientes.AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new ClienteComContagem(c.Id, c.Nome, c.Observacoes, c.CriadoEm, c.AtualizadoEm, c.Projetos.Count))
            .ToListAsync(ct);

    public async Task<ProjetoCliente?> ObterClienteAsync(int id, CancellationToken ct = default) =>
        await db.ProjetoClientes.Include(c => c.Projetos).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<ProjetoCliente?> ObterClienteComProjetosEPecasAsync(int id, CancellationToken ct = default) =>
        await db.ProjetoClientes
            .Include(c => c.Projetos).ThenInclude(p => p.Pecas)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<int> CriarClienteAsync(ProjetoCliente cliente, CancellationToken ct = default)
    {
        db.ProjetoClientes.Add(cliente);
        await db.SaveChangesAsync(ct);
        return cliente.Id;
    }

    public async Task<bool> AtualizarClienteAsync(int id, string nome, string? observacoes, DateTime atualizadoEm, CancellationToken ct = default)
    {
        var cliente = await db.ProjetoClientes.FindAsync([id], ct);
        if (cliente is null) return false;

        cliente.Nome = nome;
        cliente.Observacoes = observacoes;
        cliente.AtualizadoEm = atualizadoEm;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ExcluirClienteAsync(int id, CancellationToken ct = default)
    {
        var cliente = await db.ProjetoClientes.FindAsync([id], ct);
        if (cliente is null) return;

        db.ProjetoClientes.Remove(cliente);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Projeto?> ObterProjetoAsync(int id, CancellationToken ct = default) =>
        await db.Projetos.Include(p => p.Pecas).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<int> CriarProjetoAsync(Projeto projeto, CancellationToken ct = default)
    {
        db.Projetos.Add(projeto);
        await db.SaveChangesAsync(ct);
        return projeto.Id;
    }

    public async Task<bool> AtualizarProjetoAsync(
        int id, string nome, string? observacoes, double? larguraTecido, double? espaco, double? margem, string? giro,
        List<ProjetoPeca> pecas, DateTime atualizadoEm, CancellationToken ct = default)
    {
        var projeto = await db.Projetos.Include(p => p.Pecas).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (projeto is null) return false;

        projeto.Nome = nome;
        projeto.Observacoes = observacoes;
        projeto.LarguraTecido = larguraTecido;
        projeto.Espaco = espaco;
        projeto.Margem = margem;
        projeto.Giro = giro;
        projeto.AtualizadoEm = atualizadoEm;

        db.ProjetoPecas.RemoveRange(projeto.Pecas);
        foreach (var peca in pecas) peca.ProjetoId = id;
        db.ProjetoPecas.AddRange(pecas);

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ExcluirProjetoAsync(int id, CancellationToken ct = default)
    {
        var projeto = await db.Projetos.FindAsync([id], ct);
        if (projeto is null) return;

        db.Projetos.Remove(projeto);
        await db.SaveChangesAsync(ct);
    }

    public Task PatchMiniaturaAsync(int pecaId, string? miniatura, CancellationToken ct = default) =>
        db.ProjetoPecas
            .Where(p => p.Id == pecaId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Miniatura, miniatura), ct);

    public async Task<HashSet<int>> ListarIdsDePecasAsync(int projetoId, CancellationToken ct = default) =>
        (await db.ProjetoPecas.AsNoTracking().Where(p => p.ProjetoId == projetoId).Select(p => p.Id).ToListAsync(ct)).ToHashSet();

    public async Task<List<string>> ListarTodosArquivosDeProjetoAsync(CancellationToken ct = default) =>
        await db.ProjetoPecas.AsNoTracking().Select(p => p.Arquivo).ToListAsync(ct);
}
