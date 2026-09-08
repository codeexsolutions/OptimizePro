using FluentAssertions;
using OptimizePro.Data.Repositorios;
using OptimizePro.Services.Configuracoes;

namespace OptimizePro.Services.Tests;

public class ConfiguracaoServiceTests : IDisposable
{
    private readonly BancoDeTeste _banco = new();
    private readonly IConfiguracaoRepository _repositorio;
    private readonly ConfiguracaoService _servico;

    public ConfiguracaoServiceTests()
    {
        _repositorio = new ConfiguracaoRepository(_banco.Db);
        _servico = new ConfiguracaoService(_repositorio);
    }

    public void Dispose() => _banco.Dispose();

    [Fact]
    public async Task ObterAsync_SemNadaSalvo_RetornaOsPadroes()
    {
        var configuracoes = await _servico.ObterAsync();

        configuracoes.Should().Be(ConfiguracoesDoApp.Padrao);
    }

    [Fact]
    public async Task SalvarEObter_RoundTripPreservaOsValores()
    {
        var novas = new ConfiguracoesDoApp("1", "212", "Oi {{nome}}!", 5000, 15000, 150);

        await _servico.SalvarAsync(novas);
        var lidas = await _servico.ObterAsync();

        lidas.Should().Be(novas);
    }

    [Fact]
    public async Task SalvarAsync_DelayMaximoMenorQueMinimo_LancaArgumentException()
    {
        var invalidas = ConfiguracoesDoApp.Padrao with { DelayMinimoMs = 20000, DelayMaximoMs = 5000 };

        var acao = () => _servico.SalvarAsync(invalidas);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SalvarAsync_CodigoPaisVazio_LancaArgumentException(string codigo)
    {
        var invalidas = ConfiguracoesDoApp.Padrao with { CodigoPaisPadrao = codigo };

        var acao = () => _servico.SalvarAsync(invalidas);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SalvarAsync_DpiZeroOuNegativo_LancaArgumentException()
    {
        var invalidas = ConfiguracoesDoApp.Padrao with { DpiPadraoDeExportacao = 0 };

        var acao = () => _servico.SalvarAsync(invalidas);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SalvarAsync_ChamadoDuasVezes_AtualizaEmVezDeDuplicar()
    {
        await _servico.SalvarAsync(ConfiguracoesDoApp.Padrao with { DddPadrao = "21" });
        await _servico.SalvarAsync(ConfiguracoesDoApp.Padrao with { DddPadrao = "31" });

        var lidas = await _servico.ObterAsync();

        lidas.DddPadrao.Should().Be("31");
    }
}
