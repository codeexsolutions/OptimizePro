using FluentAssertions;
using Xunit;

namespace OptimizePro.Central.Tests;

public class PainelDeStaffServiceTests
{
    [Fact]
    public async Task ListarInstalacoes_TrazTodasComSeusAdministradores()
    {
        var instalacoes = new RepositorioDeInstalacaoFalso();
        var dados = new RepositorioDeDadoSincronizadoFalso();
        var usuarios = new UsuarioAdminService(dados);

        var inst1 = await instalacoes.CriarAsync(new Instalacao
        {
            Id = "", ClienteIdHash = 1, ChaveDeApiHash = [1], Codigo = "AAA111", NomeDaFabrica = "Fábrica 1",
        });
        var inst2 = await instalacoes.CriarAsync(new Instalacao
        {
            Id = "", ClienteIdHash = 2, ChaveDeApiHash = [2], Codigo = "BBB222", NomeDaFabrica = "Fábrica 2",
        });

        await usuarios.CadastrarAsync(inst1.Id, "dono1", "Dono da Fábrica 1", "senha123456", [], true);
        await usuarios.CadastrarAsync(inst1.Id, "op1", "Operador", "senha123456", ["Historico"], false);
        // inst2 não tem nenhum usuário ainda (instalação provisionada, painel nunca configurado).

        var servico = new PainelDeStaffService(instalacoes, usuarios);

        var resultado = await servico.ListarInstalacoesAsync();

        resultado.Should().HaveCount(2);

        var dto1 = resultado.Single(i => i.Id == inst1.Id);
        dto1.Codigo.Should().Be("AAA111");
        dto1.Administradores.Should().ContainSingle(a => a.Login == "dono1");

        var dto2 = resultado.Single(i => i.Id == inst2.Id);
        dto2.Administradores.Should().BeEmpty();
    }

    [Fact]
    public async Task ListarInstalacoes_NuncaExpoeHashDeSenha()
    {
        var instalacoes = new RepositorioDeInstalacaoFalso();
        var dados = new RepositorioDeDadoSincronizadoFalso();
        var usuarios = new UsuarioAdminService(dados);
        var inst = await instalacoes.CriarAsync(new Instalacao { Id = "", ClienteIdHash = 1, ChaveDeApiHash = [1], Codigo = "AAA111" });
        await usuarios.CadastrarAsync(inst.Id, "dono", "Dono", "senha123456", [], true);

        var servico = new PainelDeStaffService(instalacoes, usuarios);

        var resultado = await servico.ListarInstalacoesAsync();

        // AdministradorDaInstalacaoDto só tem Id/Login/Nome/Habilitado — checagem de tipo
        // garante isso em tempo de compilação, mas o teste documenta a intenção: staff vê
        // QUEM administra cada instalação, nunca a senha.
        var administrador = resultado.Single().Administradores.Single();
        administrador.Login.Should().Be("dono");
    }
}
