using FluentAssertions;
using OptimizePro.Core.Encaixe;
using OptimizePro.Core.Encaixe.Busca;

namespace OptimizePro.Core.Tests.Encaixe.Busca;

public class ParticionamentoDeFatiasTests
{
    private static Receita ReceitaContorno(CriterioDeOrdem ordem) =>
        Receita.DeContorno(AgrupamentoDeEncaixe.Solta, ordem, HeuristicaDeContorno.Fundo);

    private static Receita ReceitaNfp() =>
        new(MotorDeEncaixe.Nfp, AgrupamentoDeEncaixe.Solta, CriterioDeOrdem.Area);

    [Theory]
    [InlineData(1, 1)]   // clamp(0,1,8)
    [InlineData(2, 1)]   // clamp(1,1,8)
    [InlineData(5, 4)]   // clamp(4,1,8)
    [InlineData(9, 8)]   // clamp(8,1,8)
    [InlineData(100, 8)] // clamp(99,1,8)
    public void NumeroDeFatias_RespeitaOClampEntre1E8ReservandoUmNucleoParaUi(int processadores, int esperado)
    {
        ParticionamentoDeFatias.NumeroDeFatias(processadores).Should().Be(esperado);
    }

    [Fact]
    public void ParticionarRoundRobin_DistribuiPorIndiceModuloNumeroDeFatias()
    {
        var r = Enumerable.Range(0, 5).Select(i => ReceitaContorno((CriterioDeOrdem)(i % 4))).ToList();

        var fatias = ParticionamentoDeFatias.ParticionarRoundRobin(r, numeroDeFatias: 2);

        fatias.Should().HaveCount(2);
        fatias[0].Should().Equal(r[0], r[2], r[4]);
        fatias[1].Should().Equal(r[1], r[3]);
    }

    [Fact]
    public void ParticionarRoundRobin_NumeroDeFatiasMenorQue1_Lanca()
    {
        var acao = () => ParticionamentoDeFatias.ParticionarRoundRobin([ReceitaContorno(CriterioDeOrdem.Area)], 0);

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ParticionarComReservaDeNfp_MenosDeTresFatias_CaiParaRoundRobinNormalSemReserva()
    {
        var receitas = new[] { ReceitaContorno(CriterioDeOrdem.Area), ReceitaNfp() };

        var (fatiasNormais, fatiaNfp) = ParticionamentoDeFatias.ParticionarComReservaDeNfp(receitas, numeroDeFatias: 2, reservarUltimaFatiaParaNfp: true);

        fatiaNfp.Should().BeEmpty();
        fatiasNormais.SelectMany(f => f).Should().BeEquivalentTo(receitas);
    }

    [Fact]
    public void ParticionarComReservaDeNfp_TresOuMaisFatiasComReserva_IsolaAsReceitasNfpNaUltimaFatia()
    {
        var contorno1 = ReceitaContorno(CriterioDeOrdem.Area);
        var contorno2 = ReceitaContorno(CriterioDeOrdem.Altura);
        var nfp1 = ReceitaNfp();

        var (fatiasNormais, fatiaNfp) = ParticionamentoDeFatias.ParticionarComReservaDeNfp(
            [contorno1, nfp1, contorno2], numeroDeFatias: 3, reservarUltimaFatiaParaNfp: true);

        fatiaNfp.Should().Equal(nfp1);
        fatiasNormais.Should().HaveCount(2); // n-1 fatias pro resto
        fatiasNormais.SelectMany(f => f).Should().BeEquivalentTo([contorno1, contorno2]);
    }

    [Fact]
    public void ParticionarComReservaDeNfp_SemReceitasNfpMasReservaAtiva_UltimaFatiaFicaVaziaMesmoAssim()
    {
        // A reserva é do "modo" (≥3 workers, reserva ativa), não condicional a existir
        // receita NFP de verdade — mesmo sem nenhuma, só n-1 fatias recebem as demais.
        var contorno1 = ReceitaContorno(CriterioDeOrdem.Area);
        var contorno2 = ReceitaContorno(CriterioDeOrdem.Altura);
        var contorno3 = ReceitaContorno(CriterioDeOrdem.Lado);

        var (fatiasNormais, fatiaNfp) = ParticionamentoDeFatias.ParticionarComReservaDeNfp(
            [contorno1, contorno2, contorno3], numeroDeFatias: 3, reservarUltimaFatiaParaNfp: true);

        fatiaNfp.Should().BeEmpty();
        fatiasNormais.Should().HaveCount(2);
    }

    [Fact]
    public void ParticionarComReservaDeNfp_ReservaDesligada_UsaTodasAsFatiasNormalmente()
    {
        var receitas = new[] { ReceitaContorno(CriterioDeOrdem.Area), ReceitaNfp(), ReceitaContorno(CriterioDeOrdem.Altura) };

        var (fatiasNormais, fatiaNfp) = ParticionamentoDeFatias.ParticionarComReservaDeNfp(receitas, numeroDeFatias: 3, reservarUltimaFatiaParaNfp: false);

        fatiaNfp.Should().BeEmpty();
        fatiasNormais.Should().HaveCount(3);
        fatiasNormais.SelectMany(f => f).Should().BeEquivalentTo(receitas);
    }
}
