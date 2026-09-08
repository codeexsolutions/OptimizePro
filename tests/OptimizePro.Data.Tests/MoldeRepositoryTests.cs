using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Arte;
using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Data.Tests;

public class MoldeRepositoryTests
{
    [Fact]
    public async Task CriarEObter_PecaComContornoEFuros_RoundTripPreservaOsPontos()
    {
        using var banco = new BancoDeTeste();
        var repo = new MoldeRepository(banco.Db);

        var molde = new Molde
        {
            Nome = "Camiseta básica",
            CriadoEm = DateTime.UtcNow,
            Pecas =
            [
                new MoldePeca
                {
                    Papel = "frente",
                    Largura = 40,
                    Altura = 60,
                    Contorno = [new(0, 0), new(40, 0), new(40, 60), new(0, 60)],
                    Furos = [[new(5, 5), new(10, 5), new(10, 10)]],
                },
            ],
        };

        var id = await repo.CriarAsync(molde);
        var carregado = await repo.ObterAsync(id);

        carregado.Should().NotBeNull();
        carregado!.Nome.Should().Be("Camiseta básica");
        carregado.Pecas.Should().HaveCount(1);
        carregado.Pecas[0].Contorno.Should().Equal(new PontoXY(0, 0), new PontoXY(40, 0), new PontoXY(40, 60), new PontoXY(0, 60));
        carregado.Pecas[0].Furos.Should().NotBeNull();
        carregado.Pecas[0].Furos![0].Should().Equal(new PontoXY(5, 5), new PontoXY(10, 5), new PontoXY(10, 10));
    }

    [Fact]
    public async Task CriarEObter_ArtePecaComAjuste_RoundTripPreservaOAjuste()
    {
        using var banco = new BancoDeTeste();
        var repo = new MoldeRepository(banco.Db);

        var ajuste = new AjusteArte(TipoArte.Arte, ModoEncaixeArte.Caber, 100, 1.5, -2.5, 90, 118.11);

        var molde = new Molde
        {
            Nome = "Regata",
            CriadoEm = DateTime.UtcNow,
            Artes =
            [
                new MoldeArte
                {
                    Nome = "Estampa floral",
                    CriadoEm = DateTime.UtcNow,
                    Pecas = [new MoldeArtePeca { Papel = "frente", Arquivo = "arte1.png", Ajuste = ajuste }],
                },
            ],
        };

        var id = await repo.CriarAsync(molde);
        var carregado = await repo.ObterAsync(id);

        carregado!.Artes[0].Pecas[0].Ajuste.Should().Be(ajuste);
    }

    [Fact]
    public async Task Listar_RetornaTodosOsMoldesSemCarregarPecas()
    {
        using var banco = new BancoDeTeste();
        var repo = new MoldeRepository(banco.Db);

        await repo.CriarAsync(new Molde { Nome = "A", CriadoEm = DateTime.UtcNow });
        await repo.CriarAsync(new Molde { Nome = "B", CriadoEm = DateTime.UtcNow });

        var lista = await repo.ListarAsync();

        lista.Should().HaveCount(2);
        lista.Select(m => m.Nome).Should().BeEquivalentTo(["A", "B"]);
    }

    [Fact]
    public async Task Excluir_RemoveOMoldeEAsPecasEArtesEmCascata()
    {
        using var banco = new BancoDeTeste();
        var repo = new MoldeRepository(banco.Db);

        var molde = new Molde
        {
            Nome = "Descartável",
            CriadoEm = DateTime.UtcNow,
            Pecas = [new MoldePeca { Papel = "frente", Largura = 1, Altura = 1, Contorno = [new(0, 0), new(1, 0), new(1, 1)] }],
        };
        var id = await repo.CriarAsync(molde);

        await repo.ExcluirAsync(id);

        (await repo.ObterAsync(id)).Should().BeNull();
        banco.Db.MoldePecas.Should().BeEmpty();
    }

    [Fact]
    public async Task Obter_IdInexistente_RetornaNulo()
    {
        using var banco = new BancoDeTeste();
        var repo = new MoldeRepository(banco.Db);

        (await repo.ObterAsync(999)).Should().BeNull();
    }
}
