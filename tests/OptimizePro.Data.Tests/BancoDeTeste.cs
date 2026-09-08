using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OptimizePro.Data;

namespace OptimizePro.Data.Tests;

/// <summary>
/// SQLite in-memory com a conexão mantida aberta (§18 da arquitetura) — cada teste tem
/// seu próprio banco isolado, sem tocar disco real.
/// </summary>
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

    /// <summary>
    /// Novo <see cref="OptimizeDbContext"/> sobre a MESMA conexão (dados persistem) — usar
    /// pra reler depois de uma operação em massa (<c>ExecuteUpdate</c>/<c>ExecuteDelete</c>),
    /// que não atualiza o change tracker de um contexto já usado; imita o padrão real de
    /// cada unidade de trabalho ter seu próprio contexto.
    /// </summary>
    public OptimizeDbContext NovoContexto() => new(_opcoes);

    public void Dispose()
    {
        Db.Dispose();
        _conexao.Dispose();
    }
}
