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
    public DbSet<Maquina> Maquinas => Set<Maquina>();
    public DbSet<RegistroDeImpressao> RegistrosDeImpressao => Set<RegistroDeImpressao>();
    public DbSet<OrdemDeServico> OrdensDeServico => Set<OrdemDeServico>();
    public DbSet<OrdemDeServicoImagem> OrdensDeServicoImagens => Set<OrdemDeServicoImagem>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<PedidoItem> PedidoItens => Set<PedidoItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigurarMolde(modelBuilder);
        ConfigurarMoldeArte(modelBuilder);
        ConfigurarProjeto(modelBuilder);
        ConfigurarEncaixeMemoria(modelBuilder);
        ConfigurarConfiguracaoApp(modelBuilder);
        ConfigurarImpressoras(modelBuilder);
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

    private static void ConfigurarImpressoras(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Maquina>(b =>
        {
            b.ToTable("maquinas");
            b.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(m => m.Nome).HasColumnName("nome").IsRequired();
            b.Property(m => m.Tipo).HasColumnName("tipo").HasConversion<string>().IsRequired();
            b.Property(m => m.Habilitada).HasColumnName("habilitada").HasDefaultValue(true);
            b.Property(m => m.Host).HasColumnName("host");
            b.Property(m => m.Ip).HasColumnName("ip");
            b.Property(m => m.CaminhoHistorico).HasColumnName("caminho_historico");
            b.Property(m => m.PastaPreview).HasColumnName("pasta_preview");
            b.Property(m => m.PastaLogAoVivo).HasColumnName("pasta_log_ao_vivo");
            b.Property(m => m.ArquivoLogAoVivo).HasColumnName("arquivo_log_ao_vivo");
            b.Property(m => m.PastaLogDeStatus).HasColumnName("pasta_log_de_status");
            b.Property(m => m.CaminhoListaDeTrabalhos).HasColumnName("caminho_lista_de_trabalhos");
            b.Property(m => m.CaminhoEstatisticasDeTinta).HasColumnName("caminho_estatisticas_de_tinta");
            b.Property(m => m.Origem).HasColumnName("origem").HasDefaultValue("manual");
            b.Property(m => m.Posicao).HasColumnName("posicao").HasDefaultValue(0);
            b.Property(m => m.DescobertaEm).HasColumnName("descoberta_em");
            b.Property(m => m.AtualizadoEm).HasColumnName("atualizado_em");
            b.Property(m => m.UltimaAssinaturaHistorico).HasColumnName("ultima_assinatura_historico");

            b.HasIndex(m => m.Host).HasDatabaseName("idx_maquinas_host");

            b.HasMany(m => m.Registros).WithOne(r => r.Maquina!).HasForeignKey(r => r.MaquinaId).OnDelete(DeleteBehavior.Cascade);
        });

        var conversorCanaisDeTinta = CriarConversorJsonAnulavel<Dictionary<string, double>>();

        modelBuilder.Entity<RegistroDeImpressao>(b =>
        {
            b.ToTable("registros_de_impressao");
            b.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(r => r.MaquinaId).HasColumnName("maquina_id");
            b.Property(r => r.NomeDaMaquina).HasColumnName("nome_da_maquina");
            b.Property(r => r.TipoDeOrigem).HasColumnName("tipo_de_origem");
            b.Property(r => r.DataHora).HasColumnName("data_hora").IsRequired();
            b.Property(r => r.Data).HasColumnName("data").IsRequired();
            b.Property(r => r.Hora).HasColumnName("hora");
            b.Property(r => r.Tarefa).HasColumnName("tarefa");
            b.Property(r => r.Passada).HasColumnName("passada");
            b.Property(r => r.Status).HasColumnName("status");
            b.Property(r => r.Cancelada).HasColumnName("cancelada").HasDefaultValue(false);
            b.Property(r => r.ComErro).HasColumnName("com_erro").HasDefaultValue(false);
            b.Property(r => r.AreaDeImpressao).HasColumnName("area_de_impressao").HasDefaultValue(0);
            b.Property(r => r.ComprimentoDeImpressao).HasColumnName("comprimento_de_impressao").HasDefaultValue(0);
            b.Property(r => r.MetricaEstimada).HasColumnName("metrica_estimada").HasDefaultValue(false);
            b.Property(r => r.Concluido).HasColumnName("concluido");
            b.Property(r => r.Total).HasColumnName("total");
            b.Property(r => r.TempoSegundos).HasColumnName("tempo_segundos").HasDefaultValue(0);
            b.Property(r => r.TintaMl).HasColumnName("tinta_ml").HasDefaultValue(0);
            b.Property(r => r.TintaExperimental).HasColumnName("tinta_experimental").HasDefaultValue(false);
            b.Property(r => r.CanaisDeTinta).HasColumnName("canais_de_tinta").HasConversion(conversorCanaisDeTinta);
            b.Property(r => r.ReferenciaDePreview).HasColumnName("referencia_de_preview");
            b.Property(r => r.PercentualDeProgresso).HasColumnName("percentual_de_progresso");
            b.Property(r => r.EstadoDoProgresso).HasColumnName("estado_do_progresso");
            b.Property(r => r.HorasDecorridas).HasColumnName("horas_decorridas");
            b.Property(r => r.EhRecorteOuMosaico).HasColumnName("eh_recorte_ou_mosaico").HasDefaultValue(false);
            b.Property(r => r.AtualizadoEm).HasColumnName("atualizado_em");

            b.HasIndex(r => new { r.MaquinaId, r.Data }).HasDatabaseName("idx_registros_maquina_data");
            b.HasIndex(r => r.Data).HasDatabaseName("idx_registros_data");
        });

        modelBuilder.Entity<OrdemDeServico>(b =>
        {
            b.ToTable("ordens_de_servico");
            b.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(o => o.NomeDoCliente).HasColumnName("nome_do_cliente").IsRequired();
            b.Property(o => o.Tecido).HasColumnName("tecido");
            b.Property(o => o.TamanhoDeImpressao).HasColumnName("tamanho_de_impressao");
            b.Property(o => o.Metros).HasColumnName("metros");
            b.Property(o => o.Operador).HasColumnName("operador");
            b.Property(o => o.Maquina).HasColumnName("maquina");
            b.Property(o => o.Data).HasColumnName("data").IsRequired();
            b.Property(o => o.Observacao).HasColumnName("observacao");
            b.Property(o => o.CriadoEm).HasColumnName("criado_em").IsRequired();

            b.HasIndex(o => o.Data).HasDatabaseName("idx_ordens_de_servico_data");

            b.HasMany(o => o.Imagens).WithOne(i => i.Ordem!).HasForeignKey(i => i.OrdemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrdemDeServicoImagem>(b =>
        {
            b.ToTable("ordens_de_servico_imagens");
            b.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(i => i.OrdemId).HasColumnName("ordem_id");
            b.Property(i => i.NomeDoArquivo).HasColumnName("nome_do_arquivo");
            b.Property(i => i.TipoMime).HasColumnName("tipo_mime").IsRequired();
            b.Property(i => i.Posicao).HasColumnName("posicao").HasDefaultValue(0);
            b.Property(i => i.EhBlusa).HasColumnName("eh_blusa").HasDefaultValue(false);
            b.Property(i => i.Quantidade).HasColumnName("quantidade");
            b.Property(i => i.Dados).HasColumnName("dados").IsRequired();

            b.HasIndex(i => i.OrdemId).HasDatabaseName("idx_ordens_de_servico_imagens_ordem");
        });

        modelBuilder.Entity<Pedido>(b =>
        {
            b.ToTable("pedidos");
            b.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(p => p.CriadoEm).HasColumnName("criado_em").IsRequired();
            b.Property(p => p.Status).HasColumnName("status").HasDefaultValue("aberto").IsRequired();
            b.Property(p => p.Observacao).HasColumnName("observacao");

            b.HasMany(p => p.Itens).WithOne(i => i.Pedido!).HasForeignKey(i => i.PedidoId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PedidoItem>(b =>
        {
            b.ToTable("pedido_itens");
            b.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(i => i.PedidoId).HasColumnName("pedido_id");
            b.Property(i => i.Posicao).HasColumnName("posicao").IsRequired();
            b.Property(i => i.RegistroId).HasColumnName("registro_id").IsRequired();
            b.Property(i => i.NomeDoCliente).HasColumnName("nome_do_cliente");
            b.Property(i => i.Tecido).HasColumnName("tecido");
            b.Property(i => i.Tarefa).HasColumnName("tarefa");
            b.Property(i => i.MaquinaId).HasColumnName("maquina_id");
            b.Property(i => i.NomeDaMaquina).HasColumnName("nome_da_maquina");
            b.Property(i => i.ComprimentoDeImpressao).HasColumnName("comprimento_de_impressao");
            b.Property(i => i.Data).HasColumnName("data");
            b.Property(i => i.OrdemId).HasColumnName("ordem_id");
            b.Property(i => i.StatusNaCalandra).HasColumnName("status_na_calandra").HasDefaultValue("pendente").IsRequired();
            b.Property(i => i.MotivoNaCalandra).HasColumnName("motivo_na_calandra");
            b.Property(i => i.MotivoPersonalizadoNaCalandra).HasColumnName("motivo_personalizado_na_calandra");
            b.Property(i => i.DataNaCalandra).HasColumnName("data_na_calandra");

            b.HasIndex(i => new { i.PedidoId, i.Posicao }).HasDatabaseName("idx_pedido_itens_pedido");
            b.HasIndex(i => i.RegistroId).HasDatabaseName("idx_pedido_itens_registro");

            b.HasOne(i => i.Ordem).WithMany().HasForeignKey(i => i.OrdemId).OnDelete(DeleteBehavior.SetNull);
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
