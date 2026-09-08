using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OptimizePro.Data;

/// <summary>
/// Só para as ferramentas de design-time (<c>dotnet ef migrations add</c>) — em produção,
/// a App monta as <see cref="DbContextOptions{OptimizeDbContext}"/> de verdade via DI,
/// com o caminho real de <c>CaminhosDoApp.BancoDeDados</c> (§15/§16 da arquitetura).
/// </summary>
public sealed class OptimizeDbContextFactory : IDesignTimeDbContextFactory<OptimizeDbContext>
{
    public OptimizeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OptimizeDbContext>();
        optionsBuilder.UseSqlite("Data Source=design-time.db");
        return new OptimizeDbContext(optionsBuilder.Options);
    }
}
