using FluentAssertions;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Core.Tests.Vetor;

public class AnalisadorDePathSvgTests
{
    [Fact]
    public void Analisar_CaminhoSoDeRetas_FazRoundTripComConversorSvg()
    {
        var caminho = new CaminhoMontado(new PontoXY(0, 0),
        [
            new SegmentoReta(new PontoXY(10, 0)),
            new SegmentoReta(new PontoXY(10, 10)),
            new SegmentoReta(new PontoXY(0, 10)),
        ]);

        var d = ConversorSvg.ParaComandoDePath(caminho);
        var analisado = AnalisadorDePathSvg.Analisar(d);

        analisado.Should().BeEquivalentTo(new[] { caminho });
    }

    [Fact]
    public void Analisar_CaminhoComBezier_FazRoundTripComConversorSvg()
    {
        var caminho = new CaminhoMontado(new PontoXY(0, 0),
        [
            new SegmentoBezier(new PontoXY(3, 1), new PontoXY(7, 1), new PontoXY(10, 0)),
        ]);

        var d = ConversorSvg.ParaComandoDePath(caminho);
        var analisado = AnalisadorDePathSvg.Analisar(d);

        analisado.Should().BeEquivalentTo(new[] { caminho });
    }

    [Fact]
    public void Analisar_CaminhoComArco_FazRoundTripComConversorSvg()
    {
        var caminho = new CaminhoMontado(new PontoXY(-5, 0),
        [
            new SegmentoArco(5, GrandeArco: false, Horario: true, new PontoXY(5, 0)),
            new SegmentoArco(5, GrandeArco: false, Horario: true, new PontoXY(-5, 0)),
        ]);

        var d = ConversorSvg.ParaComandoDePath(caminho);
        var analisado = AnalisadorDePathSvg.Analisar(d);

        analisado.Should().BeEquivalentTo(new[] { caminho });
    }

    [Fact]
    public void Analisar_ExternoComFuro_DevolveDoisSubcaminhosSeparados()
    {
        var externo = new CaminhoMontado(new PontoXY(0, 0),
        [
            new SegmentoReta(new PontoXY(10, 0)),
            new SegmentoReta(new PontoXY(10, 10)),
            new SegmentoReta(new PontoXY(0, 10)),
        ]);
        var furo = new CaminhoMontado(new PontoXY(3, 3),
        [
            new SegmentoReta(new PontoXY(7, 3)),
            new SegmentoReta(new PontoXY(7, 7)),
            new SegmentoReta(new PontoXY(3, 7)),
        ]);

        var d = ConversorSvg.ParaComandoDePathComFuros([externo, furo]);
        var analisado = AnalisadorDePathSvg.Analisar(d);

        analisado.Should().BeEquivalentTo(new[] { externo, furo });
    }

    [Fact]
    public void Analisar_ComandoRelativoOuAtalho_Lanca()
    {
        var acao = () => AnalisadorDePathSvg.Analisar("M0,0 l10,0 Z");

        acao.Should().Throw<FormatException>();
    }

    [Fact]
    public void Analisar_QuadraticaConvertidaParaCubicaEquivalente_ChegaNoMesmoPontoFinal()
    {
        var analisado = AnalisadorDePathSvg.Analisar("M0,0 Q5,10 10,0 Z");

        analisado.Should().ContainSingle();
        var bezier = analisado[0].Segmentos.Should().ContainSingle().Subject.Should().BeOfType<SegmentoBezier>().Subject;
        bezier.Fim.Should().Be(new PontoXY(10, 0));
    }
}
