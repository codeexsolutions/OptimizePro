using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OptimizePro.Painel;

/// <summary>
/// Só para as ferramentas de design-time (<c>dotnet ef migrations add</c>) — em produção,
/// quem monta as <see cref="DbContextOptions{PainelDbContext}"/> de verdade é o host que expõe
/// o painel (ainda não existe — chega na fase de scaffold do React/API), com o mesmo
/// <c>CaminhosDoApp.BancoDeDados</c> do resto do app.
/// </summary>
public sealed class PainelDbContextFactory : IDesignTimeDbContextFactory<PainelDbContext>
{
    public PainelDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PainelDbContext>();
        optionsBuilder.UseSqlite("Data Source=design-time.db",
            x => x.MigrationsHistoryTable("__EFMigrationsHistory_Painel"));
        return new PainelDbContext(optionsBuilder.Options);
    }
}
