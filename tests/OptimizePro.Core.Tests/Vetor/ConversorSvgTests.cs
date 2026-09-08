using FluentAssertions;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Core.Tests.Vetor;

public class ConversorSvgTests
{
    [Fact]
    public void ParaComandoDePath_RetanguloDeRetas_GeraDComandosMLZ()
    {
        var caminho = new CaminhoMontado(new PontoXY(0, 0),
        [
            new SegmentoReta(new PontoXY(10, 0)),
            new SegmentoReta(new PontoXY(10, 5)),
            new SegmentoReta(new PontoXY(0, 5)),
            new SegmentoReta(new PontoXY(0, 0)),
        ]);

        var d = ConversorSvg.ParaComandoDePath(caminho);

        d.Should().Be("M0,0 L10,0 L10,5 L0,5 L0,0 Z");
    }

    [Fact]
    public void ParaComandoDePath_ComArco_UsaComandoAComFlagsNaOrdemCorreta()
    {
        var caminho = new CaminhoMontado(new PontoXY(10, 0),
        [
            new SegmentoArco(Raio: 10, GrandeArco: true, Horario: true, Fim: new PontoXY(10, 0)),
        ]);

        var d = ConversorSvg.ParaComandoDePath(caminho);

        d.Should().Be("M10,0 A10,10 0 1 1 10,0 Z");
    }

    [Fact]
    public void ParaComandoDePath_CaminhoVazio_DevolveStringVazia()
    {
        ConversorSvg.ParaComandoDePath(new CaminhoMontado(new PontoXY(0, 0), [])).Should().BeEmpty();
    }

    [Fact]
    public void ParaComandoDePathComFuros_ConcatenaVariosContornosIgnorandoOsVazios()
    {
        var externo = new CaminhoMontado(new PontoXY(0, 0), [new SegmentoReta(new PontoXY(10, 0))]);
        var vazio = new CaminhoMontado(new PontoXY(0, 0), []);
        var furo = new CaminhoMontado(new PontoXY(2, 2), [new SegmentoReta(new PontoXY(3, 2))]);

        var d = ConversorSvg.ParaComandoDePathComFuros([externo, vazio, furo]);

        d.Should().Be("M0,0 L10,0 Z M2,2 L3,2 Z");
    }

    [Fact]
    public void MontarSvg_MontaDocumentoComCamadasEIgnoraCaminhosVazios()
    {
        var svg = ConversorSvg.MontarSvg(100, 50,
        [
            new ConversorSvg.CamadaSvg("M0,0 L10,0 Z", new CorRgb(255, 0, 0)),
            new ConversorSvg.CamadaSvg("", new CorRgb(0, 255, 0)),
        ]);

        svg.Should().StartWith("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"50\" viewBox=\"0 0 100 50\">");
        svg.Should().Contain("fill=\"#FF0000\"");
        svg.Should().NotContain("#00FF00");
        svg.Should().EndWith("</svg>");
    }
}
