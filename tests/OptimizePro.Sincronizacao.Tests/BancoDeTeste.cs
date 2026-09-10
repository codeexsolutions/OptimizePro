using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OptimizePro.Data;
using OptimizePro.Painel;

namespace OptimizePro.Sincronizacao.Tests;

/// <summary>Os dois DbContexts (chão de fábrica + painel) sobre a MESMA conexão SQLite em memória — como no arquivo dados.db real, só que descartável. `Migrate()` em vez de `EnsureCreated()` porque os dois compartilham a conexão e cada um precisa aplicar seu próprio histórico de migrations, igual à produção (ver App.axaml.cs).</summary>
public sealed class BancoDeTeste : IDisposable
{
    private readonly SqliteConnection _conexao;

    public OptimizeDbContext Operacional { get; }
    public PainelDbContext Painel { get; }

    public BancoDeTeste()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();

        Operacional = new OptimizeDbContext(new DbContextOptionsBuilder<OptimizeDbContext>().UseSqlite(_conexao).Options);
        Operacional.Database.Migrate();

        Painel = new PainelDbContext(new DbContextOptionsBuilder<PainelDbContext>()
            .UseSqlite(_conexao, x => x.MigrationsHistoryTable("__EFMigrationsHistory_Painel")).Options);
        Painel.Database.Migrate();
    }

    public void Dispose()
    {
        Operacional.Dispose();
        Painel.Dispose();
        _conexao.Dispose();
    }
}
