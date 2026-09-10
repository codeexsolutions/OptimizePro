using FluentAssertions;

namespace OptimizePro.Central.Tests;

public class InstalacaoServiceTests
{
    [Fact]
    public async Task Provisionar_PrimeiraVez_CriaEDevolveAChaveDeApi()
    {
        var service = new InstalacaoService(new RepositorioDeInstalacaoFalso());

        var resultado = await service.ProvisionarAsync(12345u, "Confecção Sol");

        resultado.JaExistia.Should().BeFalse();
        resultado.ChaveDeApi.Should().NotBeNullOrEmpty();
        resultado.Instalacao.Id.Should().NotBeNullOrEmpty();
        resultado.Instalacao.ClienteIdHash.Should().Be(12345);
        resultado.Instalacao.NomeDaFabrica.Should().Be("Confecção Sol");
    }

    [Fact]
    public async Task Provisionar_MesmoClienteIdHashDeNovo_EhIdempotenteENaoReemiteChave()
    {
        var repo = new RepositorioDeInstalacaoFalso();
        var service = new InstalacaoService(repo);
        var primeiro = await service.ProvisionarAsync(12345u, "Confecção Sol");

        var segundo = await service.ProvisionarAsync(12345u, "Nome Diferente Da Segunda Vez");

        segundo.JaExistia.Should().BeTrue();
        segundo.ChaveDeApi.Should().BeNull("a chave só existe em texto puro no instante da criação");
        segundo.Instalacao.Id.Should().Be(primeiro.Instalacao.Id, "não deve duplicar a instalação pro mesmo ClienteIdHash");
    }

    [Fact]
    public async Task Provisionar_ClienteIdHashesDiferentes_CriaInstalacoesSeparadas()
    {
        var service = new InstalacaoService(new RepositorioDeInstalacaoFalso());

        var a = await service.ProvisionarAsync(111u, "Fábrica A");
        var b = await service.ProvisionarAsync(222u, "Fábrica B");

        a.Instalacao.Id.Should().NotBe(b.Instalacao.Id);
    }

    [Fact]
    public async Task Autenticar_ChaveCorreta_DevolveAInstalacao()
    {
        var service = new InstalacaoService(new RepositorioDeInstalacaoFalso());
        var provisionada = await service.ProvisionarAsync(999u, "Fábrica");

        var autenticada = await service.AutenticarAsync(provisionada.Instalacao.Id, provisionada.ChaveDeApi!);

        autenticada.Should().NotBeNull();
        autenticada!.Id.Should().Be(provisionada.Instalacao.Id);
    }

    [Fact]
    public async Task Autenticar_ChaveErrada_DevolveNulo()
    {
        var service = new InstalacaoService(new RepositorioDeInstalacaoFalso());
        var provisionada = await service.ProvisionarAsync(999u, "Fábrica");

        (await service.AutenticarAsync(provisionada.Instalacao.Id, "chave-errada")).Should().BeNull();
    }

    [Fact]
    public async Task Autenticar_InstalacaoInexistente_DevolveNulo()
    {
        var service = new InstalacaoService(new RepositorioDeInstalacaoFalso());

        (await service.AutenticarAsync("nao-existe", "qualquer-coisa")).Should().BeNull();
    }
}
