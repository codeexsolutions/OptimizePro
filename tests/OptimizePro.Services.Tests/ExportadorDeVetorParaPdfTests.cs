using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Services.Vetor;

namespace OptimizePro.Services.Tests;

public class ExportadorDeVetorParaPdfTests
{
    [Fact]
    public void CalcularArco_SemicirculoPequeno_AchaCentroERaioCorretos()
    {
        var arco = ExportadorDeVetorParaPdf.CalcularArco(
            new PontoXY(-5, 0), raio: 5, grandeArco: false, horario: true, new PontoXY(5, 0));

        arco.Should().NotBeNull();
        arco!.Value.CentroX.Should().BeApproximately(0, 1e-9);
        arco.Value.CentroY.Should().BeApproximately(0, 1e-9);
        arco.Value.Raio.Should().BeApproximately(5, 1e-9);
        Math.Abs(arco.Value.VarreduraGraus).Should().BeApproximately(180, 1e-6);
    }

    [Fact]
    public void CalcularArco_QuartoDeCirculoPequeno_VarreduraDe90GrausNoSentidoCerto()
    {
        var arco = ExportadorDeVetorParaPdf.CalcularArco(
            new PontoXY(5, 0), raio: 5, grandeArco: false, horario: true, new PontoXY(0, 5));

        arco.Should().NotBeNull();
        arco!.Value.CentroX.Should().BeApproximately(0, 1e-9);
        arco.Value.CentroY.Should().BeApproximately(0, 1e-9);
        arco.Value.VarreduraGraus.Should().BeApproximately(90, 1e-6); // pequeno + horário -> +90°
    }

    [Fact]
    public void CalcularArco_QuartoDeCirculoGrande_VarreduraReflexaDe270GrausMasMesmoRaio()
    {
        var arco = ExportadorDeVetorParaPdf.CalcularArco(
            new PontoXY(5, 0), raio: 5, grandeArco: true, horario: true, new PontoXY(0, 5));

        arco.Should().NotBeNull();
        arco!.Value.Raio.Should().BeApproximately(5, 1e-9);
        Math.Abs(arco.Value.VarreduraGraus).Should().BeApproximately(270, 1e-6); // arco grande -> reflexo

        // O centro muda (é a OUTRA solução do par início/fim/raio) mas continua à distância do raio dos dois pontos.
        var distInicio = Math.Sqrt(Math.Pow(5 - arco.Value.CentroX, 2) + Math.Pow(0 - arco.Value.CentroY, 2));
        var distFim = Math.Sqrt(Math.Pow(0 - arco.Value.CentroX, 2) + Math.Pow(5 - arco.Value.CentroY, 2));
        distInicio.Should().BeApproximately(5, 1e-9);
        distFim.Should().BeApproximately(5, 1e-9);
    }

    [Fact]
    public void CalcularArco_PontosIguais_DevolveNulo()
    {
        ExportadorDeVetorParaPdf.CalcularArco(new PontoXY(1, 1), 5, false, true, new PontoXY(1, 1)).Should().BeNull();
    }

    [Fact]
    public void Converter_SvgComUmaCamada_ProduzPdfValido()
    {
        var svg = """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10"><path d="M0,0 L10,0 L10,10 L0,10 Z" fill="#FF0000" fill-rule="evenodd"/></svg>""";

        var pdf = ExportadorDeVetorParaPdf.Converter(svg, 10, 10);

        pdf.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Converter_SvgComArco_NaoLancaEProduzPdfValido()
    {
        var svg = """<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 20 20"><path d="M5,10 A5,5 0 1 1 15,10 A5,5 0 1 1 5,10 Z" fill="#00FF00" fill-rule="evenodd"/></svg>""";

        var acao = () => ExportadorDeVetorParaPdf.Converter(svg, 20, 20);

        acao.Should().NotThrow();
        System.Text.Encoding.ASCII.GetString(acao(), 0, 5).Should().Be("%PDF-");
    }
}
