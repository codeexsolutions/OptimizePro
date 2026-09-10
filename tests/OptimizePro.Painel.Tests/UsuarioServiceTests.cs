using FluentAssertions;

namespace OptimizePro.Painel.Tests;

public class UsuarioServiceTests
{
    [Fact]
    public async Task Cadastrar_GuardaOHashNaoASenha()
    {
        using var banco = new BancoDeTeste();
        var repo = new UsuarioRepository(banco.Db);
        var service = new UsuarioService(repo);

        var usuario = await service.CadastrarAsync("dono", "Dono da Fábrica", "senha123", [ModuloDoPainel.Impressoras], ehAdministrador: true);

        usuario.Id.Should().NotBeNullOrEmpty();
        usuario.SenhaHash.Should().NotBeEmpty();
        HashDeSenha.Conferir("senha123", usuario.SenhaHash, usuario.SenhaSal).Should().BeTrue();

        var salvo = banco.Db.Usuarios.Single();
        System.Text.Encoding.UTF8.GetString(salvo.SenhaHash).Should().NotContain("senha123");
    }

    [Fact]
    public async Task Cadastrar_LoginDuplicado_LancaLoginJaExisteException()
    {
        using var banco = new BancoDeTeste();
        var service = new UsuarioService(new UsuarioRepository(banco.Db));
        await service.CadastrarAsync("dono", "Dono", "senha123", [], false);

        var acao = () => service.CadastrarAsync("dono", "Outro Nome", "outraSenha", [], false);

        await acao.Should().ThrowAsync<LoginJaExisteException>();
    }

    [Theory]
    [InlineData("", "Nome", "senha")]
    [InlineData("login", "", "senha")]
    [InlineData("login", "Nome", "")]
    public async Task Cadastrar_CampoObrigatorioFaltando_LancaArgumentException(string login, string nome, string senha)
    {
        using var banco = new BancoDeTeste();
        var service = new UsuarioService(new UsuarioRepository(banco.Db));

        var acao = () => service.CadastrarAsync(login, nome, senha, [], false);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ContarHabilitados_SoContaQuemEstaHabilitado()
    {
        using var banco = new BancoDeTeste();
        var service = new UsuarioService(new UsuarioRepository(banco.Db));
        var u1 = await service.CadastrarAsync("u1", "Um", "senha123", [], false);
        await service.CadastrarAsync("u2", "Dois", "senha123", [], false);
        await service.CadastrarAsync("u3", "Três", "senha123", [], false);

        await service.AtualizarHabilitadoAsync(u1.Id, false);

        (await service.ContarHabilitadosAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Atualizar_TrocaNomeModulosEAdministrador()
    {
        using var banco = new BancoDeTeste();
        var service = new UsuarioService(new UsuarioRepository(banco.Db));
        var usuario = await service.CadastrarAsync("op1", "Operador", "senha123", [ModuloDoPainel.Historico], false);

        var ok = await service.AtualizarAsync(usuario.Id, "Operador Sênior", [ModuloDoPainel.Historico, ModuloDoPainel.Pedidos], true);

        ok.Should().BeTrue();
        var atualizado = banco.Db.Usuarios.Single(u => u.Id == usuario.Id);
        atualizado.Nome.Should().Be("Operador Sênior");
        atualizado.ModulosLiberados.Should().BeEquivalentTo([ModuloDoPainel.Historico, ModuloDoPainel.Pedidos]);
        atualizado.EhAdministrador.Should().BeTrue();
    }

    [Fact]
    public async Task RedefinirSenha_TrocaOHashEAContinuaAutenticandoComANova()
    {
        using var banco = new BancoDeTeste();
        var service = new UsuarioService(new UsuarioRepository(banco.Db));
        var auth = new AutenticacaoService(new UsuarioRepository(banco.Db));
        var usuario = await service.CadastrarAsync("op1", "Operador", "senhaAntiga", [], false);

        await service.RedefinirSenhaAsync(usuario.Id, "senhaNova");

        (await auth.AutenticarAsync("op1", "senhaAntiga")).Sucesso.Should().BeFalse();
        (await auth.AutenticarAsync("op1", "senhaNova")).Sucesso.Should().BeTrue();
    }

    [Fact]
    public async Task Excluir_RemoveOUsuario()
    {
        using var banco = new BancoDeTeste();
        var service = new UsuarioService(new UsuarioRepository(banco.Db));
        var usuario = await service.CadastrarAsync("op1", "Operador", "senha123", [], false);

        (await service.ExcluirAsync(usuario.Id)).Should().BeTrue();
        (await service.ObterAsync(usuario.Id)).Should().BeNull();
        (await service.ExcluirAsync(usuario.Id)).Should().BeFalse();
    }
}
