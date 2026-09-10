using FluentAssertions;
using OptimizePro.Core.Impressoras;
using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Data.Tests;

public class RegistroDeImpressaoRepositoryTests
{
    private static Maquina NovaMaquina(string id = "imp01") => new()
    {
        Id = id,
        Nome = "Impressora 01",
        Tipo = TipoDeMaquina.Csv,
        Origem = "manual",
    };

    [Fact]
    public async Task SalvarLote_MaquinaInexistente_IgnoraORegistro()
    {
        using var banco = new BancoDeTeste();
        var repo = new RegistroDeImpressaoRepository(banco.Db);

        var gravados = await repo.SalvarLoteAsync([
            new RegistroDeImpressao { Id = "r1", MaquinaId = "fantasma", DataHora = "2026-01-01 10:00:00", Data = "2026-01-01" },
        ]);

        gravados.Should().Be(0);
        banco.Db.RegistrosDeImpressao.Should().BeEmpty();
    }

    [Fact]
    public async Task SalvarLote_RegistroNovo_Insere()
    {
        using var banco = new BancoDeTeste();
        banco.Db.Maquinas.Add(NovaMaquina());
        await banco.Db.SaveChangesAsync();

        var repo = new RegistroDeImpressaoRepository(banco.Db);
        var gravados = await repo.SalvarLoteAsync([
            new RegistroDeImpressao { Id = "r1", MaquinaId = "imp01", DataHora = "2026-01-01 10:00:00", Data = "2026-01-01", TintaMl = 5 },
        ]);

        gravados.Should().Be(1);
        banco.Db.RegistrosDeImpressao.Should().ContainSingle(r => r.Id == "r1" && r.TintaMl == 5);
    }

    [Fact]
    public async Task SalvarLote_ReleituraDoCsvAposContadorExato_NaoApagaATintaExata()
    {
        using var banco = new BancoDeTeste();
        banco.Db.Maquinas.Add(NovaMaquina());
        await banco.Db.SaveChangesAsync();

        var repo = new RegistroDeImpressaoRepository(banco.Db);

        // O monitor ao vivo já gravou o contador CMYK exato (canais preenchidos, não-experimental).
        await repo.SalvarLoteAsync([
            new RegistroDeImpressao
            {
                Id = "r1", MaquinaId = "imp01", DataHora = "2026-01-01 10:00:00", Data = "2026-01-01",
                TipoDeOrigem = "csv", TintaMl = 12.5, TintaExperimental = false,
                CanaisDeTinta = new Dictionary<string, double> { ["C"] = 3, ["M"] = 3, ["Y"] = 3, ["K"] = 3.5 },
            },
        ]);

        // Releitura do histórico chega depois com uma estimativa por área (experimental).
        var gravados = await repo.SalvarLoteAsync([
            new RegistroDeImpressao
            {
                Id = "r1", MaquinaId = "imp01", DataHora = "2026-01-01 10:00:00", Data = "2026-01-01",
                TipoDeOrigem = "csv", TintaMl = 999, TintaExperimental = true, CanaisDeTinta = null,
                ComprimentoDeImpressao = 3.2, // um campo comum qualquer deve atualizar normalmente
            },
        ]);

        gravados.Should().Be(1);
        var salvo = banco.Db.RegistrosDeImpressao.Single(r => r.Id == "r1");
        salvo.TintaMl.Should().Be(12.5, "o contador exato não pode ser sobrescrito por uma estimativa");
        salvo.TintaExperimental.Should().BeFalse();
        salvo.CanaisDeTinta.Should().NotBeNull();
        salvo.ComprimentoDeImpressao.Should().Be(3.2, "campos que não são de tinta atualizam normalmente");
    }

    [Fact]
    public async Task SalvarLote_NovoSemCanaisQuandoExistenteTinha_MantemOsCanaisAntigos()
    {
        using var banco = new BancoDeTeste();
        banco.Db.Maquinas.Add(NovaMaquina());
        await banco.Db.SaveChangesAsync();

        var repo = new RegistroDeImpressaoRepository(banco.Db);

        await repo.SalvarLoteAsync([
            new RegistroDeImpressao
            {
                Id = "r1", MaquinaId = "imp01", DataHora = "2026-01-01 10:00:00", Data = "2026-01-01",
                TipoDeOrigem = "xml", TintaMl = 8, CanaisDeTinta = new Dictionary<string, double> { ["C"] = 2 },
            },
        ]);

        // A importação de histórico não traz divisão por canal — não pode zerar o que já existia.
        await repo.SalvarLoteAsync([
            new RegistroDeImpressao { Id = "r1", MaquinaId = "imp01", DataHora = "2026-01-01 10:00:00", Data = "2026-01-01", TipoDeOrigem = "xml", TintaMl = 50, CanaisDeTinta = null },
        ]);

        var salvo = banco.Db.RegistrosDeImpressao.Single(r => r.Id == "r1");
        salvo.CanaisDeTinta.Should().ContainKey("C");
        salvo.TintaMl.Should().Be(8);
    }

    [Fact]
    public async Task ListarIntervalo_FiltraPorDataEPorMaquina()
    {
        using var banco = new BancoDeTeste();
        banco.Db.Maquinas.AddRange(NovaMaquina("imp01"), NovaMaquina("imp02"));
        await banco.Db.SaveChangesAsync();

        var repo = new RegistroDeImpressaoRepository(banco.Db);
        await repo.SalvarLoteAsync([
            new RegistroDeImpressao { Id = "r1", MaquinaId = "imp01", DataHora = "2026-01-10 10:00:00", Data = "2026-01-10" },
            new RegistroDeImpressao { Id = "r2", MaquinaId = "imp01", DataHora = "2026-01-20 10:00:00", Data = "2026-01-20" },
            new RegistroDeImpressao { Id = "r3", MaquinaId = "imp02", DataHora = "2026-01-15 10:00:00", Data = "2026-01-15" },
            new RegistroDeImpressao { Id = "r4", MaquinaId = "imp01", DataHora = "2026-02-01 10:00:00", Data = "2026-02-01" },
        ]);

        var doPeriodo = await repo.ListarIntervaloAsync(null, "2026-01-01", "2026-01-31");
        doPeriodo.Select(r => r.Id).Should().BeEquivalentTo(["r1", "r2", "r3"]);

        var soImp01 = await repo.ListarIntervaloAsync("imp01", "2026-01-01", "2026-01-31");
        soImp01.Select(r => r.Id).Should().BeEquivalentTo(["r1", "r2"]);
    }
}
