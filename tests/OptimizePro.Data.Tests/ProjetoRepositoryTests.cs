using FluentAssertions;
using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Data.Tests;

public class ProjetoRepositoryTests
{
    [Fact]
    public async Task CriarClienteEProjeto_HierarquiaCompletaERecuperavel()
    {
        using var banco = new BancoDeTeste();
        var repo = new ProjetoRepository(banco.Db);

        var clienteId = await repo.CriarClienteAsync(new ProjetoCliente { Nome = "Cliente X", CriadoEm = DateTime.UtcNow });

        var projeto = new Projeto
        {
            ClienteId = clienteId,
            Nome = "Coleção verão",
            LarguraTecido = 150,
            Giro = "180",
            CriadoEm = DateTime.UtcNow,
            Pecas = [new ProjetoPeca { Nome = "Manga", Arquivo = "manga.png", Largura = 20, Altura = 30, Quantidade = 2 }],
        };
        var projetoId = await repo.CriarProjetoAsync(projeto);

        var cliente = await repo.ObterClienteAsync(clienteId);
        cliente!.Projetos.Should().ContainSingle(p => p.Id == projetoId);

        var carregado = await repo.ObterProjetoAsync(projetoId);
        carregado!.Nome.Should().Be("Coleção verão");
        carregado.Pecas.Should().ContainSingle(p => p.Nome == "Manga" && p.Quantidade == 2);
    }

    [Fact]
    public async Task PatchMiniatura_AlteraSoAColunaMiniaturaSemTocarNoResto()
    {
        using var banco = new BancoDeTeste();
        var repo = new ProjetoRepository(banco.Db);

        var clienteId = await repo.CriarClienteAsync(new ProjetoCliente { Nome = "C", CriadoEm = DateTime.UtcNow });
        var projeto = new Projeto
        {
            ClienteId = clienteId,
            Nome = "P",
            CriadoEm = DateTime.UtcNow,
            Pecas = [new ProjetoPeca { Nome = "Peça", Arquivo = "a.png", Largura = 10, Altura = 10, Ordem = 3 }],
        };
        await repo.CriarProjetoAsync(projeto);
        var pecaId = projeto.Pecas[0].Id;

        await repo.PatchMiniaturaAsync(pecaId, "data:image/png;base64,ABC");

        // ExecuteUpdateAsync não atualiza o change tracker do contexto original — relê
        // com um contexto novo, como aconteceria numa unidade de trabalho de verdade.
        using var contextoNovo = banco.NovoContexto();
        var repoNovo = new ProjetoRepository(contextoNovo);
        var recarregado = await repoNovo.ObterProjetoAsync(projeto.Id);
        var peca = recarregado!.Pecas.Single();
        peca.Miniatura.Should().Be("data:image/png;base64,ABC");
        peca.Nome.Should().Be("Peça"); // resto intacto
        peca.Ordem.Should().Be(3);
    }

    [Fact]
    public async Task ExcluirCliente_RemoveProjetosEPecasEmCascata()
    {
        using var banco = new BancoDeTeste();
        var repo = new ProjetoRepository(banco.Db);

        var clienteId = await repo.CriarClienteAsync(new ProjetoCliente { Nome = "C", CriadoEm = DateTime.UtcNow });
        await repo.CriarProjetoAsync(new Projeto
        {
            ClienteId = clienteId,
            Nome = "P",
            CriadoEm = DateTime.UtcNow,
            Pecas = [new ProjetoPeca { Nome = "X", Arquivo = "x.png", Largura = 1, Altura = 1 }],
        });

        await repo.ExcluirClienteAsync(clienteId);

        (await repo.ObterClienteAsync(clienteId)).Should().BeNull();
        banco.Db.Projetos.Should().BeEmpty();
        banco.Db.ProjetoPecas.Should().BeEmpty();
    }
}
