using System.Text.Json;
using FluentAssertions;

namespace OptimizePro.Central.Tests;

public class AutenticacaoDeUsuarioServiceTests
{
    private static string SerializarUsuario(string id, string login, string nome, bool ehAdministrador, bool habilitado, List<string> modulos, byte[] hash, byte[] sal) =>
        JsonSerializer.Serialize(new UsuarioSincronizadoDto(id, login, nome, ehAdministrador, habilitado, modulos, hash, sal));

    private async Task<(string Codigo, RepositorioDeInstalacaoFalso Instalacoes, RepositorioDeDadoSincronizadoFalso Dados, AutenticacaoDeUsuarioService Servico)> NovoCenarioComUsuarioAsync(
        string login = "dono", string senha = "senha123", bool habilitado = true, bool ehAdministrador = true)
    {
        var repoInstalacoes = new RepositorioDeInstalacaoFalso();
        var servicoInstalacoes = new InstalacaoService(repoInstalacoes, new RepositorioDeChaveDeMaquinaFalso());
        var provisionada = await servicoInstalacoes.ProvisionarAsync(12345u, "maquina-1", "Fábrica Teste");

        var repoDados = new RepositorioDeDadoSincronizadoFalso();
        var (hash, sal) = GerarHash(senha);
        await repoDados.SalvarLoteAsync(provisionada.Instalacao.Id, [
            new ItemSincronizado(TipoDeDadoSincronizado.Usuario, "u1",
                SerializarUsuario("u1", login, "Dono", ehAdministrador, habilitado, ["Historico", "Pedidos"], hash, sal),
                DateTime.UtcNow),
        ]);

        var servico = new AutenticacaoDeUsuarioService(repoInstalacoes, repoDados);
        return (provisionada.Instalacao.Codigo, repoInstalacoes, repoDados, servico);
    }

    private static (byte[] Hash, byte[] Sal) GerarHash(string senha)
    {
        var sal = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(sal);
        var hash = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(senha, sal, 210_000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        return (hash, sal);
    }

    [Fact]
    public async Task Autenticar_CredenciaisCorretas_RetornaSucessoComOsModulos()
    {
        var (codigo, _, _, servico) = await NovoCenarioComUsuarioAsync();

        var resultado = await servico.AutenticarAsync(codigo, "dono", "senha123");

        resultado.Sucesso.Should().BeTrue();
        resultado.Usuario.Should().NotBeNull();
        resultado.Usuario!.Login.Should().Be("dono");
        resultado.Usuario.EhAdministrador.Should().BeTrue();
        resultado.Usuario.ModulosLiberados.Should().BeEquivalentTo(["Historico", "Pedidos"]);
    }

    [Fact]
    public async Task Autenticar_CodigoDeEmpresaErrado_FalhaComMensagemGenerica()
    {
        var (_, _, _, servico) = await NovoCenarioComUsuarioAsync();

        var resultado = await servico.AutenticarAsync("ZZZZZZ", "dono", "senha123");

        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().Be("Empresa, login ou senha inválidos.");
    }

    [Fact]
    public async Task Autenticar_SenhaErrada_MesmaMensagemGenericaDoCodigoErrado()
    {
        var (codigo, _, _, servico) = await NovoCenarioComUsuarioAsync();

        var resultado = await servico.AutenticarAsync(codigo, "dono", "senhaErrada");

        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().Be("Empresa, login ou senha inválidos.");
    }

    [Fact]
    public async Task Autenticar_LoginNaoEncontradoNaInstalacao_MesmaMensagemGenerica()
    {
        var (codigo, _, _, servico) = await NovoCenarioComUsuarioAsync();

        var resultado = await servico.AutenticarAsync(codigo, "naoexiste", "senha123");

        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().Be("Empresa, login ou senha inválidos.");
    }

    [Fact]
    public async Task Autenticar_UsuarioDesabilitado_Falha()
    {
        var (codigo, _, _, servico) = await NovoCenarioComUsuarioAsync(habilitado: false);

        var resultado = await servico.AutenticarAsync(codigo, "dono", "senha123");

        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().Be("Este usuário está desativado.");
    }

    [Fact]
    public async Task Autenticar_MesmoLoginEmDuasInstalacoes_SoAchaOUsuarioDaInstalacaoCorreta()
    {
        var repoInstalacoes = new RepositorioDeInstalacaoFalso();
        var servicoInstalacoes = new InstalacaoService(repoInstalacoes, new RepositorioDeChaveDeMaquinaFalso());
        var instalacaoA = await servicoInstalacoes.ProvisionarAsync(111u, "maquina-a", "Fábrica A");
        var instalacaoB = await servicoInstalacoes.ProvisionarAsync(222u, "maquina-b", "Fábrica B");

        var repoDados = new RepositorioDeDadoSincronizadoFalso();
        var (hashA, salA) = GerarHash("senhaDaFabricaA");
        var (hashB, salB) = GerarHash("senhaDaFabricaB");
        await repoDados.SalvarLoteAsync(instalacaoA.Instalacao.Id, [
            new ItemSincronizado(TipoDeDadoSincronizado.Usuario, "u1", SerializarUsuario("u1", "dono", "Dono A", true, true, [], hashA, salA), DateTime.UtcNow),
        ]);
        await repoDados.SalvarLoteAsync(instalacaoB.Instalacao.Id, [
            new ItemSincronizado(TipoDeDadoSincronizado.Usuario, "u1", SerializarUsuario("u1", "dono", "Dono B", true, true, [], hashB, salB), DateTime.UtcNow),
        ]);

        var servico = new AutenticacaoDeUsuarioService(repoInstalacoes, repoDados);

        // A senha da fábrica B não vale pro código da fábrica A, mesmo com o mesmo login "dono".
        (await servico.AutenticarAsync(instalacaoA.Instalacao.Codigo, "dono", "senhaDaFabricaB")).Sucesso.Should().BeFalse();

        var resultadoCorreto = await servico.AutenticarAsync(instalacaoA.Instalacao.Codigo, "dono", "senhaDaFabricaA");
        resultadoCorreto.Sucesso.Should().BeTrue();
        resultadoCorreto.Usuario!.Nome.Should().Be("Dono A", "o código da instalação A não pode enxergar o usuário da instalação B");
    }
}
