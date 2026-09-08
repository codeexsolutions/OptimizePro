using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OptimizePro.Core;
using OptimizePro.Core.Arte;
using OptimizePro.Data.Entidades;

namespace OptimizePro.Data;

/// <summary>
/// Porte do schema SQLite documentado em §3 da especificação (§5.3 da arquitetura).
/// Campos JSON em colunas TEXT (contorno/furos/ajuste, §3.12/§5.2) são mapeados como
/// tipos ricos via value converter, em vez de cada Service reimplementar
/// JsonSerializer.Serialize/Deserialize manualmente.
/// </summary>
public class OptimizeDbContext(DbContextOptions<OptimizeDbContext> options) : DbContext(options)
{
    public DbSet<Molde> Moldes => Set<Molde>();
    public DbSet<MoldePeca> MoldePecas => Set<MoldePeca>();
    public DbSet<MoldeArte> MoldeArtes => Set<MoldeArte>();
    public DbSet<MoldeArtePeca> MoldeArtePecas => Set<MoldeArtePeca>();
    public DbSet<ProjetoCliente> ProjetoClientes => Set<ProjetoCliente>();
    public DbSet<Projeto> Projetos => Set<Projeto>();
    public DbSet<ProjetoPeca> ProjetoPecas => Set<ProjetoPeca>();
    public DbSet<EncaixeReceita> EncaixeReceitas => Set<EncaixeReceita>();
    public DbSet<EncaixeGuardado> EncaixeGuardados => Set<EncaixeGuardado>();
    public DbSet<EncaixeHistorico> EncaixeHistoricos => Set<EncaixeHistorico>();
    public DbSet<EncaixeRedePesos> EncaixeRedePesos => Set<EncaixeRedePesos>();
    public DbSet<ConfiguracaoApp> ConfiguracoesApp => Set<ConfiguracaoApp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigurarMolde(modelBuilder);
        ConfigurarMoldeArte(modelBuilder);
        ConfigurarProjeto(modelBuilder);
        ConfigurarEncaixeMemoria(modelBuilder);
        ConfigurarConfiguracaoApp(modelBuilder);
    }

    private static void ConfigurarConfiguracaoApp(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfiguracaoApp>(b =>
        {
            b.ToTable("configuracao_app");
            b.HasKey(c => c.Chave);
            b.Property(c => c.Chave).HasColumnName("chave");
            b.Property(c => c.Valor).HasColumnName("valor");
        });
    }

    private static void ConfigurarMolde(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Molde>(b =>
        {
            b.ToTable("moldes");
            b.Property(m => m.Id).HasColumnName("id");
            b.Property(m => m.Nome).HasColumnName("nome").IsRequired();
            b.Property(m => m.Observacoes).HasColumnName("observacoes");
            b.Property(m => m.CriadoEm).HasColumnName("criado_em").IsRequired();
            b.Property(m => m.AtualizadoEm).HasColumnName("atualizado_em");

            b.HasMany(m => m.Pecas).WithOne(p => p.Molde!).HasForeignKey(p => p.MoldeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(m => m.Artes).WithOne(a => a.Molde!).HasForeignKey(a => a.MoldeId).OnDelete(DeleteBehavior.Cascade);
        });

        var conversorContorno = CriarConversorJson<List<PontoXY>>(() => []);
        var comparadorContorno = CriarComparadorDeLista<PontoXY>();

        var conversorFuros = CriarConversorJsonAnulavel<List<List<PontoXY>>>();

        modelBuilder.Entity<MoldePeca>(b =>
        {
            b.ToTable("molde_pecas");
            b.Property(p => p.Id).HasColumnName("id");
            b.Property(p => p.MoldeId).HasColumnName("molde_id");
            b.Property(p => p.Tamanho).HasColumnName("tamanho").HasDefaultValue("único").IsRequired();
            b.Property(p => p.Papel).HasColumnName("papel").IsRequired();
            b.Property(p => p.Nome).HasColumnName("nome");
            b.Property(p => p.Quantidade).HasColumnName("quantidade").HasDefaultValue(1);
            b.Property(p => p.Largura).HasColumnName("largura").IsRequired();
            b.Property(p => p.Altura).HasColumnName("altura").IsRequired();

            b.Property(p => p.Contorno).HasColumnName("contorno").IsRequired()
                .HasConversion(conversorContorno).Metadata.SetValueComparer(comparadorContorno);

            b.Property(p => p.Furos).HasColumnName("furos").HasConversion(conversorFuros);

            b.Property(p => p.Origem).HasColumnName("origem");
            b.Property(p => p.Ordem).HasColumnName("ordem").HasDefaultValue(0);
        });
    }

    private static void ConfigurarMoldeArte(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MoldeArte>(b =>
        {
            b.ToTable("molde_artes");
            b.Property(a => a.Id).HasColumnName("id");
            b.Property(a => a.MoldeId).HasColumnName("molde_id");
            b.Property(a => a.Nome).HasColumnName("nome").IsRequired();
            b.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();
            b.Property(a => a.AtualizadoEm).HasColumnName("atualizado_em");

            b.HasMany(a => a.Pecas).WithOne(p => p.Arte!).HasForeignKey(p => p.ArteId).OnDelete(DeleteBehavior.Cascade);
        });

        var conversorAjuste = CriarConversorJsonAnulavel<AjusteArte>();

        modelBuilder.Entity<MoldeArtePeca>(b =>
        {
            b.ToTable("molde_arte_pecas");
            b.Property(p => p.Id).HasColumnName("id");
            b.Property(p => p.ArteId).HasColumnName("arte_id");
            b.Property(p => p.Papel).HasColumnName("papel").IsRequired();
            b.Property(p => p.Arquivo).HasColumnName("arquivo").IsRequired();
            b.Property(p => p.NomeOriginal).HasColumnName("nome_original");
            b.Property(p => p.Ajuste).HasColumnName("ajuste").HasConversion(conversorAjuste);
        });
    }

    private static void ConfigurarProjeto(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjetoCliente>(b =>
        {
            b.ToTable("projeto_clientes");
            b.Property(c => c.Id).HasColumnName("id");
            b.Property(c => c.Nome).HasColumnName("nome").IsRequired();
            b.Property(c => c.Observacoes).HasColumnName("observacoes");
            b.Property(c => c.CriadoEm).HasColumnName("criado_em").IsRequired();
            b.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");

            b.HasMany(c => c.Projetos).WithOne(p => p.Cliente!).HasForeignKey(p => p.ClienteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Projeto>(b =>
        {
            b.ToTable("projetos");
            b.Property(p => p.Id).HasColumnName("id");
            b.Property(p => p.ClienteId).HasColumnName("cliente_id");
            b.Property(p => p.Nome).HasColumnName("nome").IsRequired();
            b.Property(p => p.Observacoes).HasColumnName("observacoes");
            b.Property(p => p.LarguraTecido).HasColumnName("largura_tecido");
            b.Property(p => p.Espaco).HasColumnName("espaco");
            b.Property(p => p.Margem).HasColumnName("margem");
            b.Property(p => p.Giro).HasColumnName("giro");
            b.Property(p => p.CriadoEm).HasColumnName("criado_em").IsRequired();
            b.Property(p => p.AtualizadoEm).HasColumnName("atualizado_em");

            b.HasIndex(p => p.ClienteId).HasDatabaseName("idx_projetos_cliente");

            b.HasMany(p => p.Pecas).WithOne(pp => pp.Projeto!).HasForeignKey(pp => pp.ProjetoId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjetoPeca>(b =>
        {
            b.ToTable("projeto_pecas");
            b.Property(p => p.Id).HasColumnName("id");
            b.Property(p => p.ProjetoId).HasColumnName("projeto_id");
            b.Property(p => p.Nome).HasColumnName("nome").IsRequired();
            b.Property(p => p.Arquivo).HasColumnName("arquivo").IsRequired();
            b.Property(p => p.Largura).HasColumnName("largura").IsRequired();
            b.Property(p => p.Altura).HasColumnName("altura").IsRequired();
            b.Property(p => p.Quantidade).HasColumnName("quantidade").HasDefaultValue(1);
            b.Property(p => p.Ordem).HasColumnName("ordem").HasDefaultValue(0);
            b.Property(p => p.Miniatura).HasColumnName("miniatura");

            b.HasIndex(p => p.ProjetoId).HasDatabaseName("idx_projeto_pecas_projeto");
        });
    }

    private static void ConfigurarEncaixeMemoria(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EncaixeReceita>(b =>
        {
            b.ToTable("encaixe_receitas");
            b.Property(r => r.Id).HasColumnName("id");
            b.Property(r => r.Assinatura).HasColumnName("assinatura").IsRequired();
            b.Property(r => r.Receita).HasColumnName("receita").IsRequired();
            b.Property(r => r.Usos).HasColumnName("usos").HasDefaultValue(0);
            b.Property(r => r.Vitorias).HasColumnName("vitorias").HasDefaultValue(0);
            b.Property(r => r.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();

            // usado em upsert ON CONFLICT (§3.5) — repositório decide inserir/atualizar por essa combinação.
            b.HasIndex(r => new { r.Assinatura, r.Receita }).IsUnique();
        });

        modelBuilder.Entity<EncaixeGuardado>(b =>
        {
            b.ToTable("encaixe_guardados");
            b.HasKey(g => g.Chave);
            b.Property(g => g.Chave).HasColumnName("chave");
            b.Property(g => g.Assinatura).HasColumnName("assinatura");
            b.Property(g => g.LarguraTecido).HasColumnName("largura_tecido");
            b.Property(g => g.Espaco).HasColumnName("espaco");
            b.Property(g => g.Margem).HasColumnName("margem");
            b.Property(g => g.Consumo).HasColumnName("consumo").IsRequired();
            b.Property(g => g.Aproveitamento).HasColumnName("aproveitamento");
            b.Property(g => g.PecasJson).HasColumnName("pecas");
            b.Property(g => g.PosicoesJson).HasColumnName("posicoes").IsRequired();
            b.Property(g => g.Receita).HasColumnName("receita");
            b.Property(g => g.CriadoEm).HasColumnName("criado_em").IsRequired();
            b.Property(g => g.AtualizadoEm).HasColumnName("atualizado_em");
        });

        modelBuilder.Entity<EncaixeHistorico>(b =>
        {
            b.ToTable("encaixe_historico");
            b.Property(h => h.Id).HasColumnName("id");
            b.Property(h => h.Assinatura).HasColumnName("assinatura").IsRequired();
            b.Property(h => h.LarguraTecido).HasColumnName("largura_tecido");
            b.Property(h => h.Pecas).HasColumnName("pecas");
            b.Property(h => h.Consumo).HasColumnName("consumo");
            b.Property(h => h.Aproveitamento).HasColumnName("aproveitamento");
            b.Property(h => h.Receita).HasColumnName("receita");
            b.Property(h => h.Tentativas).HasColumnName("tentativas");
            b.Property(h => h.CriadoEm).HasColumnName("criado_em").IsRequired();
            b.Property(h => h.FeaturesJson).HasColumnName("features");
            b.Property(h => h.PlacarJson).HasColumnName("placar");
        });

        modelBuilder.Entity<EncaixeRedePesos>(b =>
        {
            b.ToTable("encaixe_rede_pesos");
            b.HasKey(r => r.Id);
            b.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(r => r.PesosJson).HasColumnName("pesos").IsRequired();
            b.Property(r => r.Exemplos).HasColumnName("exemplos").HasDefaultValue(0);
            b.Property(r => r.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
        });
    }

    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<T, string> CriarConversorJson<T>(Func<T> valorPadraoSeNulo) =>
        new(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<T>(v, (JsonSerializerOptions?)null) ?? valorPadraoSeNulo());

    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<T?, string?> CriarConversorJsonAnulavel<T>() where T : class =>
        new(
            v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => v == null ? null : JsonSerializer.Deserialize<T>(v, (JsonSerializerOptions?)null));

    private static ValueComparer<List<T>> CriarComparadorDeLista<T>() =>
        new(
            (a, b) => (a ?? new List<T>()).SequenceEqual(b ?? new List<T>()),
            v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item!.GetHashCode())),
            v => v.ToList());
}
