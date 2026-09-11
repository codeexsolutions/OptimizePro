using FluentAssertions;
using Xunit;

namespace OptimizePro.Central.Tests;

public class AdministradorServiceTests
{
    private static AdministradorService NovoServico() => new(new RepositorioDeAdministradorFalso());

    [Fact]
    public async Task Bootstrap_PrimeiroAdministrador_Cria()
    {
        var servico = NovoServico();

        var criado = await servico.BootstrapAsync("Dono@Empresa.com", "Dono", "senha123456");

        criado.Email.Should().Be("dono@empresa.com", "normaliza pra minúsculo, senão duas capitalizações diferentes de um e-mail entram como duas contas");
    }

    [Fact]
    public async Task Bootstrap_SegundaVez_Lanca()
    {
        var servico = NovoServico();
        await servico.BootstrapAsync("dono@empresa.com", "Dono", "senha123456");

        var act = () => servico.BootstrapAsync("outro@empresa.com", "Outro", "senha654321");

        await act.Should().ThrowAsync<JaTemAdministradorException>();
    }

    [Fact]
    public async Task Autenticar_CredenciaisCorretas_Sucesso()
    {
        var servico = NovoServico();
        await servico.BootstrapAsync("dono@empresa.com", "Dono", "senha123456");

        var resultado = await servico.AutenticarAsync("dono@empresa.com", "senha123456");

        resultado.Sucesso.Should().BeTrue();
        resultado.Administrador!.Email.Should().Be("dono@empresa.com");
    }

    [Fact]
    public async Task Autenticar_SenhaErrada_Falha()
    {
        var servico = NovoServico();
        await servico.BootstrapAsync("dono@empresa.com", "Dono", "senha123456");

        var resultado = await servico.AutenticarAsync("dono@empresa.com", "senha-errada");

        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().Be("E-mail ou senha inválidos.");
    }

    [Fact]
    public async Task Autenticar_EmailInexistente_MesmaMensagemGenerica()
    {
        var servico = NovoServico();

        var resultado = await servico.AutenticarAsync("ninguem@empresa.com", "qualquer-senha");

        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().Be("E-mail ou senha inválidos.");
    }
}
