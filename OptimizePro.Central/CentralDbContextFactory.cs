using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OptimizePro.Central;

/// <summary>
/// Só para as ferramentas de design-time (<c>dotnet ef migrations add/database update</c>) —
/// lê a MESMA configuração que <c>Program.cs</c> usaria em runtime (appsettings +
/// user-secrets + variáveis de ambiente), pra "database update" aplicar de verdade no banco
/// real (ex.: Supabase) sem precisar duplicar a string de conexão em outro lugar.
/// </summary>
public sealed class CentralDbContextFactory : IDesignTimeDbContextFactory<CentralDbContext>
{
    public CentralDbContext CreateDbContext(string[] args)
    {
        var configuracao = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<CentralDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuracao.GetConnectionString("Central");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Configure a connection string \"Central\" via `dotnet user-secrets set ConnectionStrings:Central \"...\"` (dentro de OptimizePro.Central) antes de rodar as ferramentas do EF Core.");

        var optionsBuilder = new DbContextOptionsBuilder<CentralDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new CentralDbContext(optionsBuilder.Options);
    }
}
