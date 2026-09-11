using FluentAssertions;
using Xunit;

namespace OptimizePro.Central.Tests;

public class UsuarioAdminServiceTests
{
    private static UsuarioAdminService NovoServico() => new(new RepositorioDeDadoSincronizadoFalso());

    [Fact]
    public async Task Cadastrar_CriaUsuarioComHashDeSenha()
    {
        var servico = NovoServico();

        var criado = await servico.CadastrarAsync("inst-1", "dono", "Dono da Fábrica", "senha-forte", ["Historico", "Maquinas"], true);

        criado.Login.Should().Be("dono");
        criado.SenhaHash.Should().NotBeEmpty();
        HashDeSenha.Conferir("senha-forte", criado.SenhaHash, criado.SenhaSal).Should().BeTrue();

        var listados = await servico.ListarAsync("inst-1");
        listados.Should().ContainSingle(u => u.Id == criado.Id);
    }

    [Fact]
    public async Task Cadastrar_LoginDuplicado_Lanca()
    {
        var servico = NovoServico();
        await servico.CadastrarAsync("inst-1", "dono", "Dono", "senha123", [], true);

        var act = () => servico.CadastrarAsync("inst-1", "dono", "Outro", "outrasenha", [], false);

        await act.Should().ThrowAsync<LoginJaExisteException>();
    }

    [Fact]
    public async Task Cadastrar_MesmoLoginEmInstalacoesDiferentes_NaoConflita()
    {
        var servico = NovoServico();
        await servico.CadastrarAsync("inst-1", "dono", "Dono 1", "senha123", [], true);

        var criado = await servico.CadastrarAsync("inst-2", "dono", "Dono 2", "senha456", [], true);

        criado.Login.Should().Be("dono");
    }

    [Fact]
    public async Task Atualizar_TrocaModulosLiberados()
    {
        var servico = NovoServico();
        var criado = await servico.CadastrarAsync("inst-1", "op1", "Operador", "senha123", ["Historico"], false);

        var ok = await servico.AtualizarAsync("inst-1", criado.Id, "Operador Editado", ["Historico", "Pedidos", "OrdensDeServico"], false);

        ok.Should().BeTrue();
        var atualizado = (await servico.ListarAsync("inst-1")).Single(u => u.Id == criado.Id);
        atualizado.Nome.Should().Be("Operador Editado");
        atualizado.ModulosLiberados.Should().BeEquivalentTo(["Historico", "Pedidos", "OrdensDeServico"]);
    }

    [Fact]
    public async Task RedefinirSenha_TrocaHashESal()
    {
        var servico = NovoServico();
        var criado = await servico.CadastrarAsync("inst-1", "op1", "Operador", "senha-antiga", [], false);

        var ok = await servico.RedefinirSenhaAsync("inst-1", criado.Id, "senha-nova");

        ok.Should().BeTrue();
        var atualizado = (await servico.ListarAsync("inst-1")).Single(u => u.Id == criado.Id);
        HashDeSenha.Conferir("senha-nova", atualizado.SenhaHash, atualizado.SenhaSal).Should().BeTrue();
        HashDeSenha.Conferir("senha-antiga", atualizado.SenhaHash, atualizado.SenhaSal).Should().BeFalse();
    }

    [Fact]
    public async Task AtualizarHabilitado_Desativa()
    {
        var servico = NovoServico();
        var criado = await servico.CadastrarAsync("inst-1", "op1", "Operador", "senha123", [], false);

        var ok = await servico.AtualizarHabilitadoAsync("inst-1", criado.Id, false);

        ok.Should().BeTrue();
        (await servico.ListarAsync("inst-1")).Single(u => u.Id == criado.Id).Habilitado.Should().BeFalse();
    }

    [Fact]
    public async Task Excluir_RemoveUsuario()
    {
        var servico = NovoServico();
        var criado = await servico.CadastrarAsync("inst-1", "op1", "Operador", "senha123", [], false);

        var ok = await servico.ExcluirAsync("inst-1", criado.Id);

        ok.Should().BeTrue();
        (await servico.ListarAsync("inst-1")).Should().BeEmpty();
    }

    [Fact]
    public async Task Bootstrap_PrimeiroUsuario_CriaAdministradorComTodosOsModulos()
    {
        var servico = NovoServico();

        var criado = await servico.BootstrapAsync("inst-1", "dono", "Dono", "senha123");

        criado.EhAdministrador.Should().BeTrue();
        criado.ModulosLiberados.Should().BeEquivalentTo(["Impressoras", "Maquinas", "Historico", "Reposicao", "Pedidos", "OrdensDeServico"]);
    }

    [Fact]
    public async Task Bootstrap_SegundaVez_Lanca()
    {
        var servico = NovoServico();
        await servico.BootstrapAsync("inst-1", "dono", "Dono", "senha123");

        var act = () => servico.BootstrapAsync("inst-1", "outro", "Outro", "senha456");

        await act.Should().ThrowAsync<JaTemUsuarioException>();
    }

    [Fact]
    public async Task Excluir_IdInexistente_RetornaFalse()
    {
        var servico = NovoServico();

        var ok = await servico.ExcluirAsync("inst-1", "nao-existe");

        ok.Should().BeFalse();
    }
}
