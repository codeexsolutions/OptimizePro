using FluentAssertions;

namespace OptimizePro.Painel.Tests;

public class AutenticacaoServiceTests
{
    private static async Task<(UsuarioService Usuarios, AutenticacaoService Auth)> NovoContexto(BancoDeTeste banco)
    {
        var repo = new UsuarioRepository(banco.Db);
        return (new UsuarioService(repo), new AutenticacaoService(repo));
    }

    [Fact]
    public async Task Autenticar_CredenciaisCorretas_RetornaSucessoEAtualizaUltimoLogin()
    {
        using var banco = new BancoDeTeste();
        var (usuarios, auth) = await NovoContexto(banco);
        var usuario = await usuarios.CadastrarAsync("dono", "Dono", "senha123", [], true);

        var resultado = await auth.AutenticarAsync("dono", "senha123");

        resultado.Sucesso.Should().BeTrue();
        resultado.Usuario.Should().NotBeNull();
        resultado.Usuario!.Id.Should().Be(usuario.Id);
        resultado.Erro.Should().BeNull();

        banco.Db.Usuarios.Single().UltimoLoginEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Autenticar_SenhaErrada_FalhaComMensagemGenerica()
    {
        using var banco = new BancoDeTeste();
        var (usuarios, auth) = await NovoContexto(banco);
        await usuarios.CadastrarAsync("dono", "Dono", "senha123", [], true);

        var resultado = await auth.AutenticarAsync("dono", "senhaErrada");

        resultado.Sucesso.Should().BeFalse();
        resultado.Usuario.Should().BeNull();
        resultado.Erro.Should().Be("Login ou senha inválidos.");
    }

    [Fact]
    public async Task Autenticar_LoginInexistente_MesmaMensagemGenericaDaSenhaErrada()
    {
        using var banco = new BancoDeTeste();
        var (_, auth) = await NovoContexto(banco);

        var resultado = await auth.AutenticarAsync("naoexiste", "qualquer");

        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().Be("Login ou senha inválidos.", "não deve dar pista se o login existe ou não");
    }

    [Fact]
    public async Task Autenticar_UsuarioDesabilitado_Falha()
    {
        using var banco = new BancoDeTeste();
        var (usuarios, auth) = await NovoContexto(banco);
        var usuario = await usuarios.CadastrarAsync("op1", "Operador", "senha123", [], false);
        await usuarios.AtualizarHabilitadoAsync(usuario.Id, false);

        var resultado = await auth.AutenticarAsync("op1", "senha123");

        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().Be("Este usuário está desativado.");
    }
}
