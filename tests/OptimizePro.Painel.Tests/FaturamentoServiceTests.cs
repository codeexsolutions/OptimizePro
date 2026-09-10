using FluentAssertions;

namespace OptimizePro.Painel.Tests;

public class FaturamentoServiceTests
{
    private static FaturamentoService NovoServico(BancoDeTeste banco) =>
        new(new ConfiguracaoDeFaturamentoRepository(banco.Db), new UsuarioRepository(banco.Db));

    [Fact]
    public async Task ObterConfiguracao_SemNadaConfigurado_RetornaPadraoComLimite7()
    {
        using var banco = new BancoDeTeste();
        var service = NovoServico(banco);

        var configuracao = await service.ObterConfiguracaoAsync();

        configuracao.ValorBaseMensal.Should().Be(0);
        configuracao.LimiteDeUsuariosNoPlano.Should().Be(7);
        banco.Db.ConfiguracoesDeFaturamento.Should().BeEmpty("obter não deve gravar nada sozinho");
    }

    [Fact]
    public async Task AtualizarConfiguracao_SalvaEPersisteOsValores()
    {
        using var banco = new BancoDeTeste();
        var service = NovoServico(banco);

        await service.AtualizarConfiguracaoAsync(valorBaseMensal: 300m, valorPorUsuarioExtra: 25m, limiteDeUsuariosNoPlano: 7);

        var configuracao = await service.ObterConfiguracaoAsync();
        configuracao.ValorBaseMensal.Should().Be(300m);
        configuracao.ValorPorUsuarioExtra.Should().Be(25m);
        configuracao.AtualizadoEm.Should().NotBeNull();
    }

    [Theory]
    [InlineData(-1, 10, 7)]
    [InlineData(10, -1, 7)]
    [InlineData(10, 10, 0)]
    public async Task AtualizarConfiguracao_ValorInvalido_LancaArgumentOutOfRangeException(decimal valorBase, decimal valorExtra, int limite)
    {
        using var banco = new BancoDeTeste();
        var service = NovoServico(banco);

        var acao = () => service.AtualizarConfiguracaoAsync(valorBase, valorExtra, limite);

        await acao.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CalcularMensalidade_DentroDoLimite_SoCobraOValorBase()
    {
        using var banco = new BancoDeTeste();
        var service = NovoServico(banco);
        var usuarios = new UsuarioService(new UsuarioRepository(banco.Db));
        await service.AtualizarConfiguracaoAsync(300m, 25m, 7);
        for (var i = 0; i < 5; i++) await usuarios.CadastrarAsync($"u{i}", $"Usuário {i}", "senha123", [], false);

        var resumo = await service.CalcularMensalidadeAsync();

        resumo.UsuariosHabilitados.Should().Be(5);
        resumo.UsuariosExtras.Should().Be(0);
        resumo.MensalidadeTotal.Should().Be(300m);
    }

    [Fact]
    public async Task CalcularMensalidade_AcimaDoLimite_CobraUsuarioExtra()
    {
        using var banco = new BancoDeTeste();
        var service = NovoServico(banco);
        var usuarios = new UsuarioService(new UsuarioRepository(banco.Db));
        await service.AtualizarConfiguracaoAsync(300m, 25m, 7);
        for (var i = 0; i < 9; i++) await usuarios.CadastrarAsync($"u{i}", $"Usuário {i}", "senha123", [], false);

        var resumo = await service.CalcularMensalidadeAsync();

        resumo.UsuariosHabilitados.Should().Be(9);
        resumo.UsuariosExtras.Should().Be(2);
        resumo.MensalidadeTotal.Should().Be(300m + 2 * 25m);
    }

    [Fact]
    public async Task CalcularMensalidade_UsuarioDesabilitadoNaoContaComoExtra()
    {
        using var banco = new BancoDeTeste();
        var service = NovoServico(banco);
        var usuarios = new UsuarioService(new UsuarioRepository(banco.Db));
        await service.AtualizarConfiguracaoAsync(300m, 25m, 7);
        var criados = new List<Usuario>();
        for (var i = 0; i < 9; i++) criados.Add(await usuarios.CadastrarAsync($"u{i}", $"Usuário {i}", "senha123", [], false));
        await usuarios.AtualizarHabilitadoAsync(criados[0].Id, false);
        await usuarios.AtualizarHabilitadoAsync(criados[1].Id, false);

        var resumo = await service.CalcularMensalidadeAsync();

        resumo.UsuariosHabilitados.Should().Be(7);
        resumo.UsuariosExtras.Should().Be(0);
        resumo.MensalidadeTotal.Should().Be(300m);
    }
}
