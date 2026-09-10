using FluentAssertions;
using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Data.Tests;

public class PedidoRepositoryTests
{
    private static PedidoItem NovoItem(string registroId, string tarefa = "trabalho") => new()
    {
        Id = "", PedidoId = "", RegistroId = registroId, Tarefa = tarefa,
    };

    [Fact]
    public async Task CriarEListar_GeraIdEPosicoesEmOrdemDeEntrada()
    {
        using var banco = new BancoDeTeste();
        var repo = new PedidoRepository(banco.Db);

        var id = await repo.CriarAsync([NovoItem("r1"), NovoItem("r2"), NovoItem("r3")], "observação teste");

        id.Should().NotBeNullOrEmpty();
        var lista = await repo.ListarAsync();
        lista.Should().ContainSingle();
        lista[0].Id.Should().Be(id);
        lista[0].Status.Should().Be("aberto");
        lista[0].QuantidadeDeItens.Should().Be(3);
        lista[0].QuantidadeOk.Should().Be(0);

        var completo = await repo.ObterAsync(id);
        completo.Should().NotBeNull();
        completo!.Itens.Select(i => i.Posicao).Should().Equal(0, 1, 2);
        completo.Itens.Select(i => i.RegistroId).Should().Equal("r1", "r2", "r3");
        completo.Itens.All(i => !string.IsNullOrEmpty(i.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task AtualizarResultadoDoItem_MarcaStatusMotivoEData()
    {
        using var banco = new BancoDeTeste();
        var repo = new PedidoRepository(banco.Db);
        var id = await repo.CriarAsync([NovoItem("r1")], null);
        var pedido = await repo.ObterAsync(id);
        var itemId = pedido!.Itens[0].Id;

        var atualizado = await repo.AtualizarResultadoDoItemAsync(id, itemId, "erro", "rasgou", null);

        atualizado.Should().NotBeNull();
        atualizado!.StatusNaCalandra.Should().Be("erro");
        atualizado.MotivoNaCalandra.Should().Be("rasgou");
        atualizado.DataNaCalandra.Should().NotBeNull();

        var lista = await repo.ListarAsync();
        lista[0].QuantidadeComErro.Should().Be(1);
    }

    [Fact]
    public async Task AtualizarResultadoDoItem_ItemDeOutroPedido_RetornaNulo()
    {
        using var banco = new BancoDeTeste();
        var repo = new PedidoRepository(banco.Db);
        var id1 = await repo.CriarAsync([NovoItem("r1")], null);
        var id2 = await repo.CriarAsync([NovoItem("r2")], null);
        var pedido2 = await repo.ObterAsync(id2);

        var resultado = await repo.AtualizarResultadoDoItemAsync(id1, pedido2!.Itens[0].Id, "ok", null, null);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task AtualizarStatus_PedidoInexistente_RetornaFalse()
    {
        using var banco = new BancoDeTeste();
        var repo = new PedidoRepository(banco.Db);

        (await repo.AtualizarStatusAsync("fantasma", "concluido")).Should().BeFalse();
    }

    [Fact]
    public async Task Excluir_RemovePedidoEItensEmCascata()
    {
        using var banco = new BancoDeTeste();
        var repo = new PedidoRepository(banco.Db);
        var id = await repo.CriarAsync([NovoItem("r1"), NovoItem("r2")], null);

        (await repo.ExcluirAsync(id)).Should().BeTrue();
        (await repo.ObterAsync(id)).Should().BeNull();
        banco.Db.PedidoItens.Should().BeEmpty();
    }

    [Fact]
    public async Task ObterPorRegistroId_AchaOItemMaisRecente()
    {
        using var banco = new BancoDeTeste();
        var repo = new PedidoRepository(banco.Db);
        await repo.CriarAsync([NovoItem("r1")], null);

        var item = await repo.ObterPorRegistroIdAsync("r1");
        item.Should().NotBeNull();
        item!.RegistroId.Should().Be("r1");

        (await repo.ObterPorRegistroIdAsync("nao-existe")).Should().BeNull();
    }
}
