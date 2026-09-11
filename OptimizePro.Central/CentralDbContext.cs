using Microsoft.EntityFrameworkCore;

namespace OptimizePro.Central;

/// <summary>Banco da Central (§24) — PostgreSQL, multi-tenant de verdade (uma linha de <see cref="Instalacao"/> por fábrica, todo o resto ganha uma coluna de tenant quando entrar na fase 4).</summary>
public class CentralDbContext(DbContextOptions<CentralDbContext> options) : DbContext(options)
{
    public DbSet<Instalacao> Instalacoes => Set<Instalacao>();
    public DbSet<DadoSincronizado> DadosSincronizados => Set<DadoSincronizado>();
    public DbSet<Administrador> Administradores => Set<Administrador>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Instalacao>(b =>
        {
            b.ToTable("instalacoes");
            b.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(i => i.Codigo).HasColumnName("codigo").IsRequired();
            b.Property(i => i.ClienteIdHash).HasColumnName("cliente_id_hash").IsRequired();
            b.Property(i => i.ChaveDeApiHash).HasColumnName("chave_de_api_hash").IsRequired();
            b.Property(i => i.NomeDaFabrica).HasColumnName("nome_da_fabrica");
            b.Property(i => i.CriadoEm).HasColumnName("criado_em").IsRequired();
            b.Property(i => i.UltimaSincronizacaoEm).HasColumnName("ultima_sincronizacao_em");

            // Uma instalação por licença — reprovisionar com o mesmo ClienteIdHash acha a
            // mesma linha em vez de duplicar (§24.1, idempotência).
            b.HasIndex(i => i.ClienteIdHash).IsUnique();
            b.HasIndex(i => i.Codigo).IsUnique();
        });

        modelBuilder.Entity<DadoSincronizado>(b =>
        {
            b.ToTable("dados_sincronizados");
            b.HasKey(d => new { d.InstalacaoId, d.Tipo, d.EntidadeId });
            b.Property(d => d.InstalacaoId).HasColumnName("instalacao_id");
            b.Property(d => d.Tipo).HasColumnName("tipo");
            b.Property(d => d.EntidadeId).HasColumnName("entidade_id");
            b.Property(d => d.DadosJson).HasColumnName("dados_json").HasColumnType("jsonb").IsRequired();
            b.Property(d => d.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();

            b.HasOne(d => d.Instalacao).WithMany().HasForeignKey(d => d.InstalacaoId).OnDelete(DeleteBehavior.Cascade);
            // Consulta mais comum do painel: "tudo de um tipo desta instalação".
            b.HasIndex(d => new { d.InstalacaoId, d.Tipo });
        });

        modelBuilder.Entity<Administrador>(b =>
        {
            b.ToTable("administradores");
            b.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(a => a.Email).HasColumnName("email").IsRequired();
            b.Property(a => a.Nome).HasColumnName("nome").IsRequired();
            b.Property(a => a.SenhaHash).HasColumnName("senha_hash").IsRequired();
            b.Property(a => a.SenhaSal).HasColumnName("senha_sal").IsRequired();
            b.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();

            b.HasIndex(a => a.Email).IsUnique();
        });
    }
}
