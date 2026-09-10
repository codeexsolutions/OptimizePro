using FluentAssertions;
using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Data.Tests;

public class OrdemDeServicoRepositoryTests
{
    private static OrdemDeServico NovaOrdem(string cliente, string data, List<OrdemDeServicoImagem>? imagens = null) => new()
    {
        Id = "",
        NomeDoCliente = cliente,
        Data = data,
        Imagens = imagens ?? [],
    };

    [Fact]
    public async Task Criar_GeraIdEPosicoesDasImagens()
    {
        using var banco = new BancoDeTeste();
        var repo = new OrdemDeServicoRepository(banco.Db);

        var ordem = NovaOrdem("Cliente A", "2026-01-10", [
            new OrdemDeServicoImagem { Id = "", OrdemId = "", TipoMime = "image/png", Dados = [1, 2, 3] },
            new OrdemDeServicoImagem { Id = "", OrdemId = "", TipoMime = "image/png", Dados = [4, 5, 6] },
        ]);

        var id = await repo.CriarAsync(ordem);

        id.Should().NotBeNullOrEmpty();
        var carregada = await repo.ObterAsync(id);
        carregada.Should().NotBeNull();
        carregada!.Imagens.Should().HaveCount(2);
        carregada.Imagens.Select(i => i.Posicao).Should().Equal(0, 1);
        carregada.Imagens.All(i => !string.IsNullOrEmpty(i.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Listar_FiltraPorClienteOuTecido()
    {
        using var banco = new BancoDeTeste();
        var repo = new OrdemDeServicoRepository(banco.Db);

        var ordem1 = NovaOrdem("Confecção Sol", "2026-01-10");
        ordem1.Tecido = "Malha PV";
        await repo.CriarAsync(ordem1);

        var ordem2 = NovaOrdem("Outra Empresa", "2026-01-11");
        ordem2.Tecido = "Viscose";
        await repo.CriarAsync(ordem2);

        var resultado = await repo.ListarAsync("sol");
        resultado.Should().ContainSingle(o => o.NomeDoCliente == "Confecção Sol");

        var porTecido = await repo.ListarAsync("viscose");
        porTecido.Should().ContainSingle(o => o.NomeDoCliente == "Outra Empresa");

        var tudo = await repo.ListarAsync();
        tudo.Should().HaveCount(2);
    }

    [Fact]
    public async Task ObterImagem_RetornaBytesETipoMime()
    {
        using var banco = new BancoDeTeste();
        var repo = new OrdemDeServicoRepository(banco.Db);
        var ordem = NovaOrdem("Cliente A", "2026-01-10", [
            new OrdemDeServicoImagem { Id = "", OrdemId = "", TipoMime = "image/jpeg", Dados = [9, 9, 9] },
        ]);
        var id = await repo.CriarAsync(ordem);
        var carregada = await repo.ObterAsync(id);
        var imagemId = carregada!.Imagens[0].Id;

        var imagem = await repo.ObterImagemAsync(id, imagemId);

        imagem.Should().NotBeNull();
        imagem!.Value.Dados.Should().Equal(9, 9, 9);
        imagem.Value.TipoMime.Should().Be("image/jpeg");

        (await repo.ObterImagemAsync(id, "nao-existe")).Should().BeNull();
    }

    [Fact]
    public async Task Excluir_RemoveOrdemEImagensEmCascata()
    {
        using var banco = new BancoDeTeste();
        var repo = new OrdemDeServicoRepository(banco.Db);
        var ordem = NovaOrdem("Cliente A", "2026-01-10", [
            new OrdemDeServicoImagem { Id = "", OrdemId = "", TipoMime = "image/png", Dados = [1] },
        ]);
        var id = await repo.CriarAsync(ordem);

        (await repo.ExcluirAsync(id)).Should().BeTrue();
        (await repo.ObterAsync(id)).Should().BeNull();
        banco.Db.OrdensDeServicoImagens.Should().BeEmpty();

        (await repo.ExcluirAsync(id)).Should().BeFalse();
    }
}
