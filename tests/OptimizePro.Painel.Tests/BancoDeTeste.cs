using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace OptimizePro.Painel.Tests;

/// <summary>SQLite in-memory com a conexão mantida aberta — mesmo padrão de <c>OptimizePro.Data.Tests</c>.</summary>
public sealed class BancoDeTeste : IDisposable
{
    private readonly SqliteConnection _conexao;

    public PainelDbContext Db { get; }

    public BancoDeTeste()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();

        var opcoes = new DbContextOptionsBuilder<PainelDbContext>().UseSqlite(_conexao).Options;
        Db = new PainelDbContext(opcoes);
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Db.Dispose();
        _conexao.Dispose();
    }
}
