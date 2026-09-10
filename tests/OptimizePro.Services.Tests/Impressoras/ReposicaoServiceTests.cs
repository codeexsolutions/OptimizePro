using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OptimizePro.Core.Impressoras;
using OptimizePro.Data;
using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;
using OptimizePro.Services.Impressoras.Historico;

namespace OptimizePro.Services.Tests.Impressoras;

public class ReposicaoServiceTests
{
    private static (OptimizeDbContext Db, SqliteConnection Conexao) NovoBanco()
    {
        var conexao = new SqliteConnection("DataSource=:memory:");
        conexao.Open();
        var opcoes = new DbContextOptionsBuilder<OptimizeDbContext>().UseSqlite(conexao).Options;
        var db = new OptimizeDbContext(opcoes);
        db.Database.EnsureCreated();
        return (db, conexao);
    }

    [Fact]
    public async Task ObterAsync_ReconheceReposicaoSemAcentoESemCaixa()
    {
        var (db, conexao) = NovoBanco();
        using var _ = conexao;
        using var __ = db;

        db.Maquinas.Add(new Maquina { Id = "imp01", Nome = "Impressora 01", Tipo = TipoDeMaquina.Csv });
        await db.SaveChangesAsync();
        var repo = new RegistroDeImpressaoRepository(db);

        await repo.SalvarLoteAsync([
            new RegistroDeImpressao { Id = "r1", MaquinaId = "imp01", DataHora = "2026-01-05 10:00:00", Data = "2026-01-05", Tarefa = "REPOSIÇÃO camiseta.prt", ComprimentoDeImpressao = 2 },
            new RegistroDeImpressao { Id = "r2", MaquinaId = "imp01", DataHora = "2026-01-06 10:00:00", Data = "2026-01-06", Tarefa = "reposicao regata.prt", ComprimentoDeImpressao = 3 },
            new RegistroDeImpressao { Id = "r3", MaquinaId = "imp01", DataHora = "2026-01-06 11:00:00", Data = "2026-01-06", Tarefa = "trabalho normal.prt", ComprimentoDeImpressao = 5 },
        ]);

        var service = new ReposicaoService(repo);
        var resposta = await service.ObterAsync();

        resposta.QuantidadeTotal.Should().Be(2);
        resposta.MetragemTotal.Should().Be(5);
    }

    [Fact]
    public async Task ObterAsync_AgrupaPorSemanaDeSegundaADomingoEOrdenaDaMaisRecente()
    {
        var (db, conexao) = NovoBanco();
        using var _ = conexao;
        using var __ = db;

        db.Maquinas.Add(new Maquina { Id = "imp01", Nome = "Impressora 01", Tipo = TipoDeMaquina.Csv });
        await db.SaveChangesAsync();
        var repo = new RegistroDeImpressaoRepository(db);

        // 2026-01-05 é segunda; 2026-01-11 é domingo da mesma semana. 2026-01-12 já é a semana seguinte.
        await repo.SalvarLoteAsync([
            new RegistroDeImpressao { Id = "r1", MaquinaId = "imp01", DataHora = "2026-01-05 10:00:00", Data = "2026-01-05", Tarefa = "reposicao a.prt", ComprimentoDeImpressao = 2 },
            new RegistroDeImpressao { Id = "r2", MaquinaId = "imp01", DataHora = "2026-01-11 10:00:00", Data = "2026-01-11", Tarefa = "reposicao b.prt", ComprimentoDeImpressao = 3 },
            new RegistroDeImpressao { Id = "r3", MaquinaId = "imp01", DataHora = "2026-01-12 10:00:00", Data = "2026-01-12", Tarefa = "reposicao c.prt", ComprimentoDeImpressao = 4 },
        ]);

        var service = new ReposicaoService(repo);
        var resposta = await service.ObterAsync();

        resposta.Semanas.Should().HaveCount(2);
        resposta.Semanas[0].InicioDaSemana.Should().Be("2026-01-12", "a semana mais recente vem primeiro");
        resposta.Semanas[0].MetragemTotal.Should().Be(4);
        resposta.Semanas[1].InicioDaSemana.Should().Be("2026-01-05");
        resposta.Semanas[1].FimDaSemana.Should().Be("2026-01-11");
        resposta.Semanas[1].MetragemTotal.Should().Be(5, "a=2 + b=3 caem na mesma semana (segunda a domingo)");
        resposta.Semanas[1].Quantidade.Should().Be(2);
    }

    [Fact]
    public async Task ObterAsync_SemReposicao_RetornaListaVazia()
    {
        var (db, conexao) = NovoBanco();
        using var _ = conexao;
        using var __ = db;

        db.Maquinas.Add(new Maquina { Id = "imp01", Nome = "Impressora 01", Tipo = TipoDeMaquina.Csv });
        await db.SaveChangesAsync();
        var repo = new RegistroDeImpressaoRepository(db);

        await repo.SalvarLoteAsync([
            new RegistroDeImpressao { Id = "r1", MaquinaId = "imp01", DataHora = "2026-01-05 10:00:00", Data = "2026-01-05", Tarefa = "trabalho normal.prt", ComprimentoDeImpressao = 2 },
        ]);

        var service = new ReposicaoService(repo);
        var resposta = await service.ObterAsync();

        resposta.QuantidadeTotal.Should().Be(0);
        resposta.Semanas.Should().BeEmpty();
    }
}
