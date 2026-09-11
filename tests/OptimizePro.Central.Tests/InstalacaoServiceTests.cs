using FluentAssertions;

namespace OptimizePro.Central.Tests;

public class InstalacaoServiceTests
{
    private static InstalacaoService NovoServico() =>
        new(new RepositorioDeInstalacaoFalso(), new RepositorioDeChaveDeMaquinaFalso());

    [Fact]
    public async Task Provisionar_PrimeiraVez_CriaEDevolveAChaveDeApi()
    {
        var service = NovoServico();

        var resultado = await service.ProvisionarAsync(12345u, "maquina-1", "Confecção Sol");

        resultado.JaExistia.Should().BeFalse();
        resultado.ChaveDeApi.Should().NotBeNullOrEmpty();
        resultado.Instalacao.Id.Should().NotBeNullOrEmpty();
        resultado.Instalacao.ClienteIdHash.Should().Be(12345);
        resultado.Instalacao.NomeDaFabrica.Should().Be("Confecção Sol");
    }

    [Fact]
    public async Task Provisionar_MesmaMaquinaDeNovo_EhIdempotenteENaoReemiteChave()
    {
        var service = NovoServico();
        var primeiro = await service.ProvisionarAsync(12345u, "maquina-1", "Confecção Sol");

        var segundo = await service.ProvisionarAsync(12345u, "maquina-1", "Nome Diferente Da Segunda Vez");

        segundo.JaExistia.Should().BeTrue();
        segundo.ChaveDeApi.Should().BeNull("a chave só existe em texto puro no instante da criação");
        segundo.Instalacao.Id.Should().Be(primeiro.Instalacao.Id, "não deve duplicar a instalação pro mesmo ClienteIdHash");
    }

    [Fact]
    public async Task Provisionar_SegundaMaquinaDaMesmaInstalacao_GanhaChavePropria()
    {
        // O cenário real: uma gráfica com várias máquinas na mesma rede, uma licença só
        // (mesmo ClienteIdHash) — cada máquina ainda precisa conseguir sincronizar sozinha.
        var service = NovoServico();
        var maquina1 = await service.ProvisionarAsync(12345u, "maquina-1", "Confecção Sol");

        var maquina2 = await service.ProvisionarAsync(12345u, "maquina-2", "Confecção Sol");

        maquina2.Instalacao.Id.Should().Be(maquina1.Instalacao.Id, "as duas máquinas compartilham a mesma instalação");
        maquina2.ChaveDeApi.Should().NotBeNullOrEmpty("a segunda máquina precisa da própria chave pra sincronizar");
        maquina2.ChaveDeApi.Should().NotBe(maquina1.ChaveDeApi);

        // As duas chaves autenticam contra a MESMA instalação.
        (await service.AutenticarAsync(maquina1.Instalacao.Id, maquina1.ChaveDeApi!)).Should().NotBeNull();
        (await service.AutenticarAsync(maquina2.Instalacao.Id, maquina2.ChaveDeApi!)).Should().NotBeNull();
    }

    [Fact]
    public async Task Provisionar_ClienteIdHashesDiferentes_CriaInstalacoesSeparadas()
    {
        var service = NovoServico();

        var a = await service.ProvisionarAsync(111u, "maquina-a", "Fábrica A");
        var b = await service.ProvisionarAsync(222u, "maquina-b", "Fábrica B");

        a.Instalacao.Id.Should().NotBe(b.Instalacao.Id);
    }

    [Fact]
    public async Task Autenticar_ChaveCorreta_DevolveAInstalacao()
    {
        var service = NovoServico();
        var provisionada = await service.ProvisionarAsync(999u, "maquina-1", "Fábrica");

        var autenticada = await service.AutenticarAsync(provisionada.Instalacao.Id, provisionada.ChaveDeApi!);

        autenticada.Should().NotBeNull();
        autenticada!.Id.Should().Be(provisionada.Instalacao.Id);
    }

    [Fact]
    public async Task Autenticar_ChaveErrada_DevolveNulo()
    {
        var service = NovoServico();
        var provisionada = await service.ProvisionarAsync(999u, "maquina-1", "Fábrica");

        (await service.AutenticarAsync(provisionada.Instalacao.Id, "chave-errada")).Should().BeNull();
    }

    [Fact]
    public async Task Autenticar_InstalacaoInexistente_DevolveNulo()
    {
        var service = NovoServico();

        (await service.AutenticarAsync("nao-existe", "qualquer-coisa")).Should().BeNull();
    }
}
