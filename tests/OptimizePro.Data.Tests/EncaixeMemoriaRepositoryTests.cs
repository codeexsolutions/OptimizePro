using FluentAssertions;
using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Data.Tests;

public class EncaixeMemoriaRepositoryTests
{
    [Fact]
    public async Task RegistrarUsoDeReceita_PrimeiraVez_CriaComUmUso()
    {
        using var banco = new BancoDeTeste();
        var repo = new EncaixeMemoriaRepository(banco.Db);

        await repo.RegistrarUsoDeReceitaAsync("l150|...", "contorno:dupla:area:fundo", venceu: true);

        var receita = await repo.ObterReceitaAsync("l150|...", "contorno:dupla:area:fundo");
        receita.Should().NotBeNull();
        receita!.Usos.Should().Be(1);
        receita.Vitorias.Should().Be(1);
    }

    [Fact]
    public async Task RegistrarUsoDeReceita_ChamadasRepetidas_FazUpsertIncrementando()
    {
        using var banco = new BancoDeTeste();
        var repo = new EncaixeMemoriaRepository(banco.Db);

        await repo.RegistrarUsoDeReceitaAsync("assinatura", "receita", venceu: true);
        await repo.RegistrarUsoDeReceitaAsync("assinatura", "receita", venceu: false);
        await repo.RegistrarUsoDeReceitaAsync("assinatura", "receita", venceu: true);

        var receita = await repo.ObterReceitaAsync("assinatura", "receita");
        receita!.Usos.Should().Be(3);
        receita.Vitorias.Should().Be(2);

        banco.Db.EncaixeReceitas.Should().ContainSingle(); // upsert, não duplicou linha
    }

    [Fact]
    public async Task SalvarGuardado_ChaveNova_Insere()
    {
        using var banco = new BancoDeTeste();
        var repo = new EncaixeMemoriaRepository(banco.Db);

        await repo.SalvarGuardadoAsync(new EncaixeGuardado
        {
            Chave = "trabalho-1",
            Consumo = 5.75,
            PosicoesJson = "[]",
            CriadoEm = DateTime.UtcNow,
        });

        var guardado = await repo.ObterGuardadoAsync("trabalho-1");
        guardado.Should().NotBeNull();
        guardado!.Consumo.Should().Be(5.75);
    }

    [Fact]
    public async Task SalvarGuardado_MesmaChaveDeNovo_Sobrescreve()
    {
        using var banco = new BancoDeTeste();
        var repo = new EncaixeMemoriaRepository(banco.Db);

        await repo.SalvarGuardadoAsync(new EncaixeGuardado { Chave = "k", Consumo = 10, PosicoesJson = "[]", CriadoEm = DateTime.UtcNow });
        await repo.SalvarGuardadoAsync(new EncaixeGuardado { Chave = "k", Consumo = 8, PosicoesJson = "[{\"x\":1}]", CriadoEm = DateTime.UtcNow });

        var guardado = await repo.ObterGuardadoAsync("k");
        guardado!.Consumo.Should().Be(8);
        guardado.PosicoesJson.Should().Be("[{\"x\":1}]");

        banco.Db.EncaixeGuardados.Should().ContainSingle();
    }

    [Fact]
    public async Task Limpar_RemoveReceitasGuardadosEHistorico()
    {
        using var banco = new BancoDeTeste();
        var repo = new EncaixeMemoriaRepository(banco.Db);

        await repo.RegistrarUsoDeReceitaAsync("a", "r", true);
        await repo.SalvarGuardadoAsync(new EncaixeGuardado { Chave = "k", Consumo = 1, PosicoesJson = "[]", CriadoEm = DateTime.UtcNow });
        await repo.RegistrarHistoricoAsync(new EncaixeHistorico { Assinatura = "a", CriadoEm = DateTime.UtcNow });

        await repo.LimparAsync();

        banco.Db.EncaixeReceitas.Should().BeEmpty();
        banco.Db.EncaixeGuardados.Should().BeEmpty();
        banco.Db.EncaixeHistoricos.Should().BeEmpty();
    }
}
