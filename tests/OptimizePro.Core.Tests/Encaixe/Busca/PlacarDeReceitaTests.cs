using FluentAssertions;
using OptimizePro.Core.Encaixe;
using OptimizePro.Core.Encaixe.Busca;

namespace OptimizePro.Core.Tests.Encaixe.Busca;

public class PlacarDeReceitaTests
{
    private static Receita ReceitaQualquer() => Receita.DeContorno(AgrupamentoDeEncaixe.Solta, CriterioDeOrdem.Area, HeuristicaDeContorno.Fundo);

    /// <summary>
    /// Regressão de um bug real (porte de melhoria do projeto de referência, 02/09/2026):
    /// comparar só por ConsumoCm deixa uma tentativa que deixou PEÇA DE FORA parecer "melhor"
    /// só porque, com menos peça, gastou menos tecido — mesmo com o encaixe incompleto. Menos
    /// itens não-encaixados sempre vence, não importa o consumo.
    /// </summary>
    [Fact]
    public void EhMelhor_MenosNaoEncaixadosVenceMesmoComConsumoMaior()
    {
        PlacarDeReceita.EhMelhor(naoEncaixadosCandidato: 0, consumoCandidato: 500, naoEncaixadosAtual: 1, consumoAtual: 100)
            .Should().BeTrue();

        PlacarDeReceita.EhMelhor(naoEncaixadosCandidato: 1, consumoCandidato: 100, naoEncaixadosAtual: 0, consumoAtual: 500)
            .Should().BeFalse();
    }

    [Fact]
    public void EhMelhor_MesmoNaoEncaixados_DesempataPorConsumo()
    {
        PlacarDeReceita.EhMelhor(naoEncaixadosCandidato: 2, consumoCandidato: 90, naoEncaixadosAtual: 2, consumoAtual: 100)
            .Should().BeTrue();

        PlacarDeReceita.EhMelhor(naoEncaixadosCandidato: 2, consumoCandidato: 110, naoEncaixadosAtual: 2, consumoAtual: 100)
            .Should().BeFalse();
    }

    [Fact]
    public void Registrar_TentativaIncompletaComConsumoMenor_NaoSubstituiUmaCompletaEDeConsumoMaior()
    {
        var placar = new PlacarDeReceita(ReceitaQualquer());

        placar.Registrar(consumoCm: 100, naoEncaixados: 0, ordem: [0, 1, 2]);
        placar.Registrar(consumoCm: 40, naoEncaixados: 1, ordem: [1, 0, 2]); // gastou menos, mas deixou 1 peça de fora.

        placar.MelhorConsumoCm.Should().Be(100);
        placar.MelhorNaoEncaixados.Should().Be(0);
        placar.MelhorOrdem.Should().Equal(0, 1, 2);
    }

    [Fact]
    public void Registrar_PrimeiraTentativaSempreViraMelhor_MesmoDeixandoPecaDeFora()
    {
        var placar = new PlacarDeReceita(ReceitaQualquer());

        placar.Registrar(consumoCm: 500, naoEncaixados: 3, ordem: [0]);

        placar.MelhorConsumoCm.Should().Be(500);
        placar.MelhorNaoEncaixados.Should().Be(3);
    }
}
