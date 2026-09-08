using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OptimizePro.Data;

namespace OptimizePro.Services.Tests;

/// <summary>SQLite in-memory com a conexão mantida aberta — mesmo padrão de <c>OptimizePro.Data.Tests</c>.</summary>
public sealed class BancoDeTeste : IDisposable
{
    private readonly SqliteConnection _conexao;
    private readonly DbContextOptions<OptimizeDbContext> _opcoes;

    public OptimizeDbContext Db { get; }

    public BancoDeTeste()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();

        _opcoes = new DbContextOptionsBuilder<OptimizeDbContext>().UseSqlite(_conexao).Options;
        Db = new OptimizeDbContext(_opcoes);
        Db.Database.EnsureCreated();
    }

    /// <summary>Novo contexto sobre a MESMA conexão — necessário pra reler após um <c>ExecuteUpdateAsync</c>, que não atualiza o change tracker de um contexto já usado (mesmo padrão de <c>OptimizePro.Data.Tests</c>).</summary>
    public OptimizeDbContext NovoContexto() => new(_opcoes);

    public void Dispose()
    {
        Db.Dispose();
        _conexao.Dispose();
    }
}
