using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace OptimizePro.Central.Tests;

public class FaturamentoServiceTests
{
    private static async Task SincronizarFaturamentoAsync(RepositorioDeDadoSincronizadoFalso dados, string instalacaoId, FaturamentoSincronizadoDto dto)
    {
        await dados.SalvarAsync(instalacaoId, new ItemSincronizado(TipoDeDadoSincronizado.Faturamento, "1", JsonSerializer.Serialize(dto), DateTime.UtcNow));
    }

    [Fact]
    public async Task Obter_SemDadosSincronizados_RetornaNull()
    {
        var dados = new RepositorioDeDadoSincronizadoFalso();
        var usuarios = new UsuarioAdminService(dados);
        var servico = new FaturamentoService(dados, usuarios);

        var resumo = await servico.ObterAsync("inst-1");

        resumo.Should().BeNull();
    }

    [Fact]
    public async Task Obter_DentroDoLimite_NaoCobraExtra()
    {
        var dados = new RepositorioDeDadoSincronizadoFalso();
        var usuarios = new UsuarioAdminService(dados);
        await usuarios.CadastrarAsync("inst-1", "op1", "Op 1", "senha123", [], false);
        await usuarios.CadastrarAsync("inst-1", "op2", "Op 2", "senha123", [], false);
        await SincronizarFaturamentoAsync(dados, "inst-1", new FaturamentoSincronizadoDto(300m, 25m, 7, new DateOnly(2026, 12, 31)));

        var servico = new FaturamentoService(dados, usuarios);
        var resumo = await servico.ObterAsync("inst-1");

        resumo.Should().NotBeNull();
        resumo!.UsuariosHabilitados.Should().Be(2);
        resumo.UsuariosExtras.Should().Be(0);
        resumo.MensalidadeTotal.Should().Be(300m);
        resumo.LicencaValidaAte.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public async Task Obter_AcimaDoLimite_CobraPorUsuarioExtra()
    {
        var dados = new RepositorioDeDadoSincronizadoFalso();
        var usuarios = new UsuarioAdminService(dados);
        for (var i = 0; i < 9; i++)
            await usuarios.CadastrarAsync("inst-1", $"op{i}", $"Op {i}", "senha123", [], false);
        await SincronizarFaturamentoAsync(dados, "inst-1", new FaturamentoSincronizadoDto(300m, 25m, 7, null));

        var servico = new FaturamentoService(dados, usuarios);
        var resumo = await servico.ObterAsync("inst-1");

        resumo!.UsuariosHabilitados.Should().Be(9);
        resumo.UsuariosExtras.Should().Be(2);
        resumo.MensalidadeTotal.Should().Be(350m); // 300 + 2*25
        resumo.LicencaValidaAte.Should().BeNull();
    }

    [Fact]
    public async Task Obter_UsuarioDesativadoNaoContaNaMensalidade()
    {
        var dados = new RepositorioDeDadoSincronizadoFalso();
        var usuarios = new UsuarioAdminService(dados);
        var criado = await usuarios.CadastrarAsync("inst-1", "op1", "Op 1", "senha123", [], false);
        await usuarios.AtualizarHabilitadoAsync("inst-1", criado.Id, false);
        await SincronizarFaturamentoAsync(dados, "inst-1", new FaturamentoSincronizadoDto(300m, 25m, 7, null));

        var servico = new FaturamentoService(dados, usuarios);
        var resumo = await servico.ObterAsync("inst-1");

        resumo!.UsuariosHabilitados.Should().Be(0);
    }

    [Fact]
    public async Task Obter_IsolaPorInstalacao()
    {
        var dados = new RepositorioDeDadoSincronizadoFalso();
        var usuarios = new UsuarioAdminService(dados);
        await usuarios.CadastrarAsync("inst-1", "op1", "Op 1", "senha123", [], false);
        await SincronizarFaturamentoAsync(dados, "inst-1", new FaturamentoSincronizadoDto(300m, 25m, 7, null));

        var servico = new FaturamentoService(dados, usuarios);
        var resumo = await servico.ObterAsync("inst-2");

        resumo.Should().BeNull();
    }
}
