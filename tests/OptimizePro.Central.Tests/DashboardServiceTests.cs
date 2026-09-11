using System.Text.Json;
using FluentAssertions;

namespace OptimizePro.Central.Tests;

public class DashboardServiceTests
{
    private static async Task<(RepositorioDeDadoSincronizadoFalso Dados, DashboardService Servico)> NovoCenarioAsync(string instalacaoId, IEnumerable<ItemSincronizado> itens)
    {
        var repo = new RepositorioDeDadoSincronizadoFalso();
        await repo.SalvarLoteAsync(instalacaoId, itens.ToList());
        return (repo, new DashboardService(repo));
    }

    private static ItemSincronizado NovoRegistro(string id, string maquinaId, string data, string dataHora, string? tarefa, double comprimento) =>
        new(TipoDeDadoSincronizado.RegistroDeImpressao, id,
            JsonSerializer.Serialize(new RegistroDeImpressaoDto(id, maquinaId, "Impressora 1", dataHora, data, tarefa, comprimento, comprimento, "Concluído", false, false, 0)),
            DateTime.UtcNow);

    [Fact]
    public async Task ObterMaquinas_DesserializaAsLinhasSincronizadas()
    {
        var (_, servico) = await NovoCenarioAsync("inst-1", [
            new ItemSincronizado(TipoDeDadoSincronizado.Maquina, "m1", JsonSerializer.Serialize(new MaquinaDto("m1", "Impressora 1", "Csv", true, "host1", "10.0.0.1")), DateTime.UtcNow),
        ]);

        var maquinas = await servico.ObterMaquinasAsync("inst-1");

        maquinas.Should().ContainSingle();
        maquinas[0].Nome.Should().Be("Impressora 1");
    }

    [Fact]
    public async Task ObterMaquinas_LinhaCorrompida_IgnoraSemDerrubarAsOutras()
    {
        var repo = new RepositorioDeDadoSincronizadoFalso();
        await repo.SalvarLoteAsync("inst-1", [
            new ItemSincronizado(TipoDeDadoSincronizado.Maquina, "m1", "{ isto não é um MaquinaDto válido nem json", DateTime.UtcNow),
            new ItemSincronizado(TipoDeDadoSincronizado.Maquina, "m2", JsonSerializer.Serialize(new MaquinaDto("m2", "Impressora 2", "Xml", true, null, null)), DateTime.UtcNow),
        ]);
        var servico = new DashboardService(repo);

        var maquinas = await servico.ObterMaquinasAsync("inst-1");

        maquinas.Should().ContainSingle(m => m.Id == "m2");
    }

    [Fact]
    public async Task ObterReposicao_AgrupaPorSemanaEIgnoraTrabalhoNormal()
    {
        var (_, servico) = await NovoCenarioAsync("inst-1", [
            NovoRegistro("r1", "m1", "2026-01-05", "2026-01-05 10:00:00", "reposicao camiseta.prt", 2),
            NovoRegistro("r2", "m1", "2026-01-11", "2026-01-11 10:00:00", "REPOSIÇÃO regata.prt", 3),
            NovoRegistro("r3", "m1", "2026-01-12", "2026-01-12 10:00:00", "reposicao proxima semana.prt", 4),
            NovoRegistro("r4", "m1", "2026-01-05", "2026-01-05 11:00:00", "trabalho normal.prt", 5),
        ]);

        var resposta = await servico.ObterReposicaoAsync("inst-1");

        resposta.QuantidadeTotal.Should().Be(3, "só os 3 com \"reposição\" no nome");
        resposta.Semanas.Should().HaveCount(2);
        resposta.Semanas[0].InicioDaSemana.Should().Be("2026-01-12", "a semana mais recente vem primeiro");
        resposta.Semanas[1].MetragemTotal.Should().Be(5, "r1 (2) + r2 (3), mesma semana (seg-dom)");
    }

    [Fact]
    public async Task ObterImpressoras_CombinaMaquinasComOTrabalhoDeHoje()
    {
        var hoje = DateTime.Now.ToString("yyyy-MM-dd");
        var (_, servico) = await NovoCenarioAsync("inst-1", [
            new ItemSincronizado(TipoDeDadoSincronizado.Maquina, "m1", JsonSerializer.Serialize(new MaquinaDto("m1", "Impressora 1", "Csv", true, null, null)), DateTime.UtcNow),
            NovoRegistro("r1", "m1", hoje, $"{hoje} 09:00:00", "trabalho A.prt", 2),
            NovoRegistro("r2", "m1", hoje, $"{hoje} 14:00:00", "trabalho B.prt", 3),
            NovoRegistro("r3", "m1", "2020-01-01", "2020-01-01 09:00:00", "trabalho antigo.prt", 100),
        ]);

        var resumo = await servico.ObterImpressorasAsync("inst-1");

        resumo.Should().ContainSingle();
        resumo[0].TrabalhosHoje.Should().Be(2, "só conta hoje, não o registro antigo");
        resumo[0].MetragemHoje.Should().Be(5);
        resumo[0].UltimoTrabalho.Should().Be("trabalho B.prt", "o mais recente por data/hora");
    }

    [Fact]
    public async Task ObterPedidos_TrazOsItensJuntoDentroDoJson()
    {
        var pedidoJson = JsonSerializer.Serialize(new PedidoDto("p1", DateTime.UtcNow, "aberto", null, [
            new PedidoItemDto("pi1", 0, "r1", "Cliente A", "Tecido", "tarefa.prt", "Impressora 1", 2.5, "2026-01-01", "pendente"),
        ]));
        var (_, servico) = await NovoCenarioAsync("inst-1", [
            new ItemSincronizado(TipoDeDadoSincronizado.Pedido, "p1", pedidoJson, DateTime.UtcNow),
        ]);

        var pedidos = await servico.ObterPedidosAsync("inst-1");

        pedidos.Should().ContainSingle();
        pedidos[0].Itens.Should().ContainSingle(i => i.NomeDoCliente == "Cliente A");
    }

    [Fact]
    public async Task ObterOrdensDeServico_DesserializaAsLinhas()
    {
        var (_, servico) = await NovoCenarioAsync("inst-1", [
            new ItemSincronizado(TipoDeDadoSincronizado.OrdemDeServico, "os1",
                JsonSerializer.Serialize(new OrdemDeServicoDto("os1", "Cliente A", "Malha", "P", 10, "João", "Impressora 1", "2026-01-01", null, 2)),
                DateTime.UtcNow),
        ]);

        var ordens = await servico.ObterOrdensDeServicoAsync("inst-1");

        ordens.Should().ContainSingle(o => o.NomeDoCliente == "Cliente A" && o.QuantidadeDeImagens == 2);
    }

    [Fact]
    public async Task ObterDados_InstalacaoDiferente_NaoEnxergaOsDadosDeOutra()
    {
        var (repo, _) = await NovoCenarioAsync("inst-A", [
            new ItemSincronizado(TipoDeDadoSincronizado.Maquina, "m1", JsonSerializer.Serialize(new MaquinaDto("m1", "Da A", "Csv", true, null, null)), DateTime.UtcNow),
        ]);
        var servicoB = new DashboardService(repo);

        var maquinasDeB = await servicoB.ObterMaquinasAsync("inst-B");

        maquinasDeB.Should().BeEmpty();
    }
}
