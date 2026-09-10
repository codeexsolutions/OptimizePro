using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace OptimizePro.Painel;

/// <summary>
/// Banco do painel do proprietário (§23) — schema e histórico de migrations PRÓPRIOS
/// (<c>__EFMigrationsHistory_Painel</c>), separados do <c>OptimizeDbContext</c> do chão de
/// fábrica, mesmo morando no mesmo arquivo <c>dados.db</c> (mesma instalação, um proprietário
/// só). A separação é de propósito (pedido do usuário, 10/09/2026): evoluir o painel não deve
/// arriscar mexer no schema operacional, e vice-versa.
/// </summary>
public class PainelDbContext(DbContextOptions<PainelDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<ConfiguracaoDeFaturamento> ConfiguracoesDeFaturamento => Set<ConfiguracaoDeFaturamento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var conversorDeModulos = new ValueConverter<List<ModuloDoPainel>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<ModuloDoPainel>>(v, (JsonSerializerOptions?)null) ?? new List<ModuloDoPainel>());

        var comparadorDeModulos = new ValueComparer<List<ModuloDoPainel>>(
            (a, b) => (a ?? new List<ModuloDoPainel>()).SequenceEqual(b ?? new List<ModuloDoPainel>()),
            v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            v => v.ToList());

        modelBuilder.Entity<Usuario>(b =>
        {
            b.ToTable("painel_usuarios");
            b.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(u => u.Login).HasColumnName("login").IsRequired();
            b.Property(u => u.Nome).HasColumnName("nome").IsRequired();
            b.Property(u => u.SenhaHash).HasColumnName("senha_hash").IsRequired();
            b.Property(u => u.SenhaSal).HasColumnName("senha_sal").IsRequired();
            b.Property(u => u.EhAdministrador).HasColumnName("eh_administrador").HasDefaultValue(false);
            b.Property(u => u.Habilitado).HasColumnName("habilitado").HasDefaultValue(true);
            b.Property(u => u.ModulosLiberados).HasColumnName("modulos_liberados").IsRequired()
                .HasConversion(conversorDeModulos).Metadata.SetValueComparer(comparadorDeModulos);
            b.Property(u => u.CriadoEm).HasColumnName("criado_em").IsRequired();
            b.Property(u => u.AtualizadoEm).HasColumnName("atualizado_em");
            b.Property(u => u.UltimoLoginEm).HasColumnName("ultimo_login_em");

            // Login é o identificador que a pessoa digita pra entrar — não pode colidir.
            b.HasIndex(u => u.Login).IsUnique();
        });

        modelBuilder.Entity<ConfiguracaoDeFaturamento>(b =>
        {
            b.ToTable("painel_faturamento");
            b.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(c => c.ValorBaseMensal).HasColumnName("valor_base_mensal").HasDefaultValue(0m);
            b.Property(c => c.ValorPorUsuarioExtra).HasColumnName("valor_por_usuario_extra").HasDefaultValue(0m);
            b.Property(c => c.LimiteDeUsuariosNoPlano).HasColumnName("limite_de_usuarios_no_plano").HasDefaultValue(7);
            b.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");
        });
    }
}
