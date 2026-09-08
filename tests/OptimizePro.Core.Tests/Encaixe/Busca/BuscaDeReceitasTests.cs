using FluentAssertions;
using OptimizePro.Core.Encaixe;
using OptimizePro.Core.Encaixe.Busca;

namespace OptimizePro.Core.Tests.Encaixe.Busca;

/// <summary>Relógio controlável manualmente, para testar parede/perseguir sem depender de tempo real.</summary>
internal sealed class RelogioFalso : IRelogioDeBusca
{
    public long TempoDecorridoMs { get; set; }
}

public class BuscaDeReceitasTests
{
    private static readonly Receita ReceitaA = Receita.DeContorno(AgrupamentoDeEncaixe.Solta, CriterioDeOrdem.Area, HeuristicaDeContorno.Fundo);
    private static readonly Receita ReceitaB = Receita.DeContorno(AgrupamentoDeEncaixe.Solta, CriterioDeOrdem.Altura, HeuristicaDeContorno.Vazio);

    private static IReadOnlyList<int> OrdemBaseFixa(CriterioDeOrdem _) => [0, 1, 2];

    /// <summary>B sempre "ganha" (consumo menor), não importa a ordem sorteada — deixa o resultado final determinístico apesar do sorteio.</summary>
    private static ResultadoDeTentativa ExecutarComResultadoFixoPorReceita(Receita receita, IReadOnlyList<int> ordem, CancellationToken _) =>
        receita == ReceitaA ? new ResultadoDeTentativa(10.0, 0) : new ResultadoDeTentativa(8.0, 0);

    private static ParametrosDeBusca ParametrosGenerosos() => new(TempoMaximoMs: 1_000_000, MsSemGanhoParaParede: 1_000_000);

    [Fact]
    public void BuscarMelhorEncaixe_TetoNaPassadaBase_RodaCadaReceitaExatamenteUmaVez()
    {
        var resultado = BuscaDeReceitas.BuscarMelhorEncaixe(
            [ReceitaA, ReceitaB], 3, OrdemBaseFixa, ExecutarComResultadoFixoPorReceita,
            ParametrosGenerosos(), new RelogioFalso(), new Random(1), tetoDeTentativasParaTeste: 2);

        resultado.Tentativas.Should().Be(2);
        resultado.MelhorReceita.Should().Be(ReceitaB);
        resultado.MelhorConsumoCm.Should().Be(8.0);
    }

    [Fact]
    public void BuscarMelhorEncaixe_ContinuaNaMelhoriaAteOTeto_AcumulaTentativas()
    {
        var resultado = BuscaDeReceitas.BuscarMelhorEncaixe(
            [ReceitaA, ReceitaB], 3, OrdemBaseFixa, ExecutarComResultadoFixoPorReceita,
            ParametrosGenerosos(), new RelogioFalso(), new Random(2), tetoDeTentativasParaTeste: 10);

        resultado.Tentativas.Should().Be(10);
        resultado.MelhorReceita.Should().Be(ReceitaB);
        resultado.MelhorConsumoCm.Should().Be(8.0);
        resultado.MelhorOrdem.Should().HaveCount(3);
    }

    [Fact]
    public void BuscarMelhorEncaixe_TempoMaximoZero_AindaAssimRodaAPassadaBase()
    {
        var relogio = new RelogioFalso { TempoDecorridoMs = 0 };
        var parametros = new ParametrosDeBusca(TempoMaximoMs: 0, MsSemGanhoParaParede: 1000);

        var resultado = BuscaDeReceitas.BuscarMelhorEncaixe(
            [ReceitaA, ReceitaB], 3, OrdemBaseFixa, ExecutarComResultadoFixoPorReceita,
            parametros, relogio, new Random(3));

        resultado.Tentativas.Should().Be(2); // só a passada base — melhoria nunca entra (0 < 0 é falso)
        resultado.MelhorConsumoCm.Should().Be(8.0);
    }

    [Fact]
    public void BuscarMelhorEncaixe_TokenJaCancelado_Lanca()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var acao = () => BuscaDeReceitas.BuscarMelhorEncaixe(
            [ReceitaA, ReceitaB], 3, OrdemBaseFixa, ExecutarComResultadoFixoPorReceita,
            ParametrosGenerosos(), new RelogioFalso(), new Random(4), cancelamento: cts.Token);

        acao.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BuscarMelhorEncaixe_SemReceitas_Lanca()
    {
        var acao = () => BuscaDeReceitas.BuscarMelhorEncaixe(
            [], 3, OrdemBaseFixa, ExecutarComResultadoFixoPorReceita,
            ParametrosGenerosos(), new RelogioFalso(), new Random(5));

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuscarMelhorEncaixe_OrdemPassadaAoExecutor_TemTamanhoIgualAQuantidadeDeItens()
    {
        var tamanhosVistos = new List<int>();

        ResultadoDeTentativa ExecutarRegistrandoTamanho(Receita r, IReadOnlyList<int> ordem, CancellationToken ct)
        {
            tamanhosVistos.Add(ordem.Count);
            return ExecutarComResultadoFixoPorReceita(r, ordem, ct);
        }

        IReadOnlyList<int> OrdemBaseDeCinco(CriterioDeOrdem _) => [0, 1, 2, 3, 4];

        BuscaDeReceitas.BuscarMelhorEncaixe(
            [ReceitaA, ReceitaB], 5, OrdemBaseDeCinco, ExecutarRegistrandoTamanho,
            ParametrosGenerosos(), new RelogioFalso(), new Random(6), tetoDeTentativasParaTeste: 8);

        tamanhosVistos.Should().AllSatisfy(t => t.Should().Be(5));
    }

    [Fact]
    public void BuscarMelhorEncaixe_Placar_TemUmaLinhaPorReceitaComTentativasEVitorias()
    {
        var resultado = BuscaDeReceitas.BuscarMelhorEncaixe(
            [ReceitaA, ReceitaB], 3, OrdemBaseFixa, ExecutarComResultadoFixoPorReceita,
            ParametrosGenerosos(), new RelogioFalso(), new Random(7), tetoDeTentativasParaTeste: 10);

        resultado.Placar.Should().HaveCount(2);

        var linhaA = resultado.Placar.Single(l => l.Receita == ReceitaA);
        var linhaB = resultado.Placar.Single(l => l.Receita == ReceitaB);

        // A roda primeiro na passada base (ordem de entrada, sem peso) e vira recorde trivial
        // por ser a primeira tentativa da busca inteira — 1 vitória, mesmo perdendo depois.
        linhaA.Tentativas.Should().BeGreaterThan(0);
        linhaA.Vitorias.Should().Be(1);

        // B bate o recorde de A na primeira vez que aparece (8.0 < 10.0) — 1 vitória. Tentativas
        // seguintes de B empatam em 8.0 (não é "<"), então não somam mais vitórias.
        linhaB.Tentativas.Should().BeGreaterThan(0);
        linhaB.Vitorias.Should().Be(1);

        (linhaA.Tentativas + linhaB.Tentativas).Should().Be(resultado.Tentativas);
    }

    [Fact]
    public void BuscarMelhorEncaixe_PesoDaReceita_RodaAPassadaBaseDaMaisPesadaPraMenosPesada()
    {
        var ordemDeExecucao = new List<Receita>();

        ResultadoDeTentativa ExecutarRegistrandoOrdem(Receita r, IReadOnlyList<int> ordem, CancellationToken ct)
        {
            ordemDeExecucao.Add(r);
            return ExecutarComResultadoFixoPorReceita(r, ordem, ct);
        }

        // Sem isto, ReceitaA rodaria primeiro (ordem de entrada); com o peso, B (peso maior) roda primeiro.
        double Peso(Receita r) => r == ReceitaB ? 10.0 : 1.0;

        BuscaDeReceitas.BuscarMelhorEncaixe(
            [ReceitaA, ReceitaB], 3, OrdemBaseFixa, ExecutarRegistrandoOrdem,
            ParametrosGenerosos(), new RelogioFalso(), new Random(8),
            tetoDeTentativasParaTeste: 2, pesoDaReceita: Peso);

        ordemDeExecucao[0].Should().Be(ReceitaB);
        ordemDeExecucao[1].Should().Be(ReceitaA);
    }

    /// <summary>
    /// Regressão de um bug real achado em produção (02/09/2026, via divergência entre "melhor
    /// até agora" no progresso da tela e o resultado final da busca): quando a MESMA receita
    /// que já é a melhor global melhora AINDA MAIS (acha uma ordem melhor pra si mesma —
    /// comum no modo "Refinar", que insiste na receita vencedora), o placar dela é o MESMO
    /// OBJETO já apontado por "melhor global" — registrar a nova tentativa já mutava o placar
    /// pro valor novo ANTES da comparação seguinte rodar, virando uma auto-comparação
    /// (`novoValor < novoValor` = sempre falso) que nunca disparava a atualização da ordem
    /// vencedora. Resultado real: `MelhorConsumoCm` batia com uma tentativa de verdade, mas
    /// `MelhorOrdem` ficava presa numa tentativa ANTERIOR (pior) da mesma receita — reconstruir
    /// com essa ordem nunca reproduzia o consumo relatado.
    /// </summary>
    [Fact]
    public void BuscarMelhorEncaixe_MesmaReceitaSeAutoSuperandoVariasVezes_MelhorOrdemAcompanhaOMelhorConsumoCm()
    {
        var chamada = 0;
        IReadOnlyList<int>? ordemDaMelhorChamadaAteAgora = null;
        var melhorConsumoVistoAteAgora = double.PositiveInfinity;

        ResultadoDeTentativa Executar(Receita r, IReadOnlyList<int> ordem, CancellationToken ct)
        {
            chamada++;
            var consumo = 100.0 - chamada; // estritamente melhor a cada chamada — só 1 receita, então é sempre ELA se auto-superando.
            if (consumo < melhorConsumoVistoAteAgora)
            {
                melhorConsumoVistoAteAgora = consumo;
                ordemDaMelhorChamadaAteAgora = ordem;
            }
            return new ResultadoDeTentativa(consumo, 0);
        }

        var resultado = BuscaDeReceitas.BuscarMelhorEncaixe(
            [ReceitaA], 3, OrdemBaseFixa, Executar,
            ParametrosGenerosos(), new RelogioFalso(), new Random(42), tetoDeTentativasParaTeste: 20);

        resultado.MelhorConsumoCm.Should().Be(melhorConsumoVistoAteAgora);
        resultado.MelhorOrdem.Should().Equal(ordemDaMelhorChamadaAteAgora);
    }

    /// <summary>
    /// Regressão de um bug real (porte de melhoria do projeto de referência, 02/09/2026):
    /// comparar só por ConsumoCm deixa a busca escolher uma tentativa que deixou peça de fora
    /// só porque, com menos peça, "gastou menos tecido" — mesmo com o encaixe incompleto.
    /// </summary>
    [Fact]
    public void BuscarMelhorEncaixe_ReceitaComPecaDeForaNuncaVenceUmaCompletaMesmoComConsumoMenor()
    {
        // A sempre encaixa tudo (consumo alto); B sempre "economiza" deixando 1 peça de fora.
        ResultadoDeTentativa ExecutarComIncompletaMaisBarata(Receita r, IReadOnlyList<int> ordem, CancellationToken ct) =>
            r == ReceitaA ? new ResultadoDeTentativa(100.0, 0) : new ResultadoDeTentativa(40.0, 1);

        var resultado = BuscaDeReceitas.BuscarMelhorEncaixe(
            [ReceitaA, ReceitaB], 3, OrdemBaseFixa, ExecutarComIncompletaMaisBarata,
            ParametrosGenerosos(), new RelogioFalso(), new Random(10), tetoDeTentativasParaTeste: 10);

        resultado.MelhorReceita.Should().Be(ReceitaA);
        resultado.MelhorConsumoCm.Should().Be(100.0);
        resultado.MelhorNaoEncaixados.Should().Be(0);
    }
}
