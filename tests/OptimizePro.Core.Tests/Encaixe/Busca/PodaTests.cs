using FluentAssertions;
using OptimizePro.Core.Encaixe;
using OptimizePro.Core.Encaixe.Busca;

namespace OptimizePro.Core.Tests.Encaixe.Busca;

public class PodaTests
{
    private static Receita ReceitaQualquer(CriterioDeOrdem ordem = CriterioDeOrdem.Area) =>
        Receita.DeContorno(AgrupamentoDeEncaixe.Solta, ordem, HeuristicaDeContorno.Fundo);

    private static PlacarDeReceita ComMelhor(double? consumo)
    {
        var placar = new PlacarDeReceita(ReceitaQualquer());
        if (consumo is { } c) placar.Registrar(c, naoEncaixados: 0, [0]);
        return placar;
    }

    [Fact]
    public void ReceitasNaRoda_PoolMenorOuIgualAoMinimo_NaoPoda()
    {
        var placares = new[] { ComMelhor(10), ComMelhor(100), ComMelhor(1000) }; // 3 <= minimo(4)

        var naRoda = Poda.ReceitasNaRoda(placares, melhorGlobalConsumoCm: 10, ativa: true);

        naRoda.Should().BeEquivalentTo(placares);
    }

    [Fact]
    public void ReceitasNaRoda_PodaInativa_NaoFiltra()
    {
        var placares = new[] { ComMelhor(10), ComMelhor(10), ComMelhor(10), ComMelhor(10), ComMelhor(1000) };

        var naRoda = Poda.ReceitasNaRoda(placares, melhorGlobalConsumoCm: 10, ativa: false);

        naRoda.Should().BeEquivalentTo(placares);
    }

    [Fact]
    public void ReceitasNaRoda_FiltraAsQueEstouramATolerancia()
    {
        // melhor=10, tolerância padrão 1.06 -> limite=10.6. 10.5 fica, 11 é cortado.
        var dentro = ComMelhor(10.5);
        var fora = ComMelhor(11.0);
        var placares = new[] { ComMelhor(10), ComMelhor(10), ComMelhor(10), dentro, fora, ComMelhor(10) };

        var naRoda = Poda.ReceitasNaRoda(placares, melhorGlobalConsumoCm: 10, ativa: true);

        naRoda.Should().Contain(dentro);
        naRoda.Should().NotContain(fora);
    }

    [Fact]
    public void ReceitasNaRoda_ReceitaNuncaTestada_NuncaEPodada()
    {
        var nuncaTestada = ComMelhor(null);
        var placares = new[] { ComMelhor(10), ComMelhor(10), ComMelhor(10), ComMelhor(1000), nuncaTestada };

        var naRoda = Poda.ReceitasNaRoda(placares, melhorGlobalConsumoCm: 10, ativa: true);

        naRoda.Should().Contain(nuncaTestada);
    }

    [Fact]
    public void ReceitasNaRoda_FiltragemEstritaDeixariaMenosQueOMinimo_CompletaComAsMelhoresRestantes()
    {
        // minimo=4; só 1 receita fica dentro da tolerância estrita, mas o piso garante 4.
        var placares = new[] { ComMelhor(10), ComMelhor(50), ComMelhor(60), ComMelhor(70), ComMelhor(80) };

        var naRoda = Poda.ReceitasNaRoda(placares, melhorGlobalConsumoCm: 10, ativa: true, minimo: 4);

        naRoda.Should().HaveCount(4);
        naRoda.Should().Contain(p => p.MelhorConsumoCm == 10);
        naRoda.Should().NotContain(p => p.MelhorConsumoCm == 80); // a pior fica de fora
    }
}
