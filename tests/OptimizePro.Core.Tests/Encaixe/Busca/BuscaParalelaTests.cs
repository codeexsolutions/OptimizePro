using FluentAssertions;
using OptimizePro.Core.Encaixe;
using OptimizePro.Core.Encaixe.Busca;

namespace OptimizePro.Core.Tests.Encaixe.Busca;

public class BuscaParalelaTests
{
    private static readonly Receita ReceitaA = Receita.DeContorno(AgrupamentoDeEncaixe.Solta, CriterioDeOrdem.Area, HeuristicaDeContorno.Fundo);
    private static readonly Receita ReceitaB = Receita.DeContorno(AgrupamentoDeEncaixe.Solta, CriterioDeOrdem.Altura, HeuristicaDeContorno.Fundo);
    private static readonly Receita ReceitaC = Receita.DeContorno(AgrupamentoDeEncaixe.Solta, CriterioDeOrdem.Lado, HeuristicaDeContorno.Fundo);
    private static readonly Receita ReceitaD = Receita.DeContorno(AgrupamentoDeEncaixe.Solta, CriterioDeOrdem.Largura, HeuristicaDeContorno.Fundo);

    // A=10, B=5, C=8, D=3 (menor) — deixa o vencedor global determinístico apesar do
    // sorteio real acontecer dentro de cada fatia.
    private static readonly Dictionary<Receita, double> Consumos = new() { [ReceitaA] = 10, [ReceitaB] = 5, [ReceitaC] = 8, [ReceitaD] = 3 };

    private static ResultadoDeTentativa Executar(Receita r, IReadOnlyList<int> ordem, CancellationToken _) => new(Consumos[r], 0);

    private static IReadOnlyList<int> OrdemBaseFixa(CriterioDeOrdem _) => [0, 1, 2];

    private static ParametrosDeBusca ParametrosGenerosos() => new(TempoMaximoMs: 1_000_000, MsSemGanhoParaParede: 1_000_000);

    [Fact]
    public async Task BuscarMelhorEncaixeAsync_DuasFatiasRoundRobin_AcheOMinimoGlobalEntreAsFatias()
    {
        // fatia0 = [A,C] (índices 0,2) -> melhor é C(8); fatia1 = [B,D] (índices 1,3) -> melhor é D(3).
        var resultado = await BuscaParalela.BuscarMelhorEncaixeAsync(
            [ReceitaA, ReceitaB, ReceitaC, ReceitaD], quantidadeDeItens: 3, OrdemBaseFixa, Executar,
            ParametrosGenerosos(), () => new RelogioFalso(), k => new Random(k + 1),
            numeroDeFatias: 2, tetoDeTentativasParaTestePorFatia: 2);

        resultado.ResultadosPorFatia.Should().HaveCount(2);
        resultado.Melhor.MelhorReceita.Should().Be(ReceitaD);
        resultado.Melhor.MelhorConsumoCm.Should().Be(3);
    }

    [Fact]
    public async Task BuscarMelhorEncaixeAsync_ComReservaDeNfpETresFatias_AindaAchaOMinimoGlobal()
    {
        var receitaNfp = new Receita(MotorDeEncaixe.Nfp, AgrupamentoDeEncaixe.Solta, CriterioDeOrdem.Area);
        var consumosComNfp = new Dictionary<Receita, double>(Consumos) { [receitaNfp] = 1 }; // NFP vence tudo aqui
        ResultadoDeTentativa ExecutarComNfp(Receita r, IReadOnlyList<int> ordem, CancellationToken _) => new(consumosComNfp[r], 0);

        var resultado = await BuscaParalela.BuscarMelhorEncaixeAsync(
            [ReceitaA, ReceitaB, ReceitaC, receitaNfp], quantidadeDeItens: 3, OrdemBaseFixa, ExecutarComNfp,
            ParametrosGenerosos(), () => new RelogioFalso(), k => new Random(k + 1),
            reservarUltimaFatiaParaNfp: true, numeroDeFatias: 3, tetoDeTentativasParaTestePorFatia: 2);

        resultado.Melhor.MelhorReceita.Should().Be(receitaNfp);
        resultado.Melhor.MelhorConsumoCm.Should().Be(1);
    }

    [Fact]
    public async Task BuscarMelhorEncaixeAsync_SemReceitas_Lanca()
    {
        var acao = async () => await BuscaParalela.BuscarMelhorEncaixeAsync(
            [], quantidadeDeItens: 3, OrdemBaseFixa, Executar,
            ParametrosGenerosos(), () => new RelogioFalso(), k => new Random(k + 1), numeroDeFatias: 2);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task BuscarMelhorEncaixeAsync_NumeroDeFatiasMaiorQueReceitas_FatiasVaziasSaoIgnoradas()
    {
        // Só 2 receitas, 5 fatias pedidas — 3 delas ficam vazias e não devem gerar tarefa/erro.
        var resultado = await BuscaParalela.BuscarMelhorEncaixeAsync(
            [ReceitaA, ReceitaD], quantidadeDeItens: 3, OrdemBaseFixa, Executar,
            ParametrosGenerosos(), () => new RelogioFalso(), k => new Random(k + 1),
            numeroDeFatias: 5, tetoDeTentativasParaTestePorFatia: 1);

        resultado.ResultadosPorFatia.Should().HaveCount(2);
        resultado.Melhor.MelhorReceita.Should().Be(ReceitaD);
    }
}
