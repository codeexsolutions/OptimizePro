using System.Text;
using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Services.Encaixe;
using PdfSharp.Pdf.IO;

namespace OptimizePro.Services.Tests;

public class ExportadorDeEncaixeParaPdfTests
{
    private static readonly IReadOnlyList<PontoXY> QuadradoDe10Cm = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];

    [Fact]
    public void UnidadeDaPagina_DentroDoTeto_Retorna1()
    {
        ExportadorDeEncaixeParaPdf.UnidadeDaPagina(500 * 2.83, 500 * 2.83).Should().Be(1);
    }

    [Fact]
    public void UnidadeDaPagina_AcimaDoTeto_CresceOSuficientePraCaber()
    {
        // Rolo de 1200 cm de comprimento — passa longe do teto de 508 cm (14400pt).
        var alturaPt = 1200 * 72.0 / 2.54;
        var unidade = ExportadorDeEncaixeParaPdf.UnidadeDaPagina(100, alturaPt);

        unidade.Should().BeGreaterThan(1);
        (alturaPt / unidade).Should().BeLessThanOrEqualTo(14400.01, "a página final, já dividida pela unidade, precisa caber no teto do formato");
    }

    [Fact]
    public void Exportar_PecaSimples_GeraPdfValido()
    {
        var posicoes = new List<ItemDeResultado> { new("p1#0", 5, 5, 10, 10, 0) };
        var contornos = new Dictionary<string, IReadOnlyList<PontoXY>> { ["p1"] = QuadradoDe10Cm };

        var bytes = ExportadorDeEncaixeParaPdf.Exportar(100, 100, posicoes, contornos);

        bytes.Should().NotBeEmpty();
        // PdfSharp já escreve 1.7 por padrão (mais novo que o mínimo 1.6 que o /UserUnit
        // exige) — não precisamos forçar a versão pra esse caso pequeno.
        Encoding.ASCII.GetString(bytes, 0, 8).Should().StartWith("%PDF-1.7");
    }

    [Fact]
    public void Exportar_RoloGigante_ForcaPdf16OuMaior()
    {
        var posicoes = new List<ItemDeResultado> { new("p1#0", 0, 0, 10, 10, 0) };
        var contornos = new Dictionary<string, IReadOnlyList<PontoXY>> { ["p1"] = QuadradoDe10Cm };

        // 1200 cm de comprimento — precisa do /UserUnit, que só existe a partir do PDF 1.6.
        var bytes = ExportadorDeEncaixeParaPdf.Exportar(100, 1200, posicoes, contornos);
        var versao = double.Parse(Encoding.ASCII.GetString(bytes, 5, 3), System.Globalization.CultureInfo.InvariantCulture);

        versao.Should().BeGreaterThanOrEqualTo(1.6);
    }

    [Fact]
    public void Exportar_PecaGirada_NaoLanca()
    {
        var posicoes = new List<ItemDeResultado> { new("p1#0", 5, 5, 10, 10, 90) };
        var contornos = new Dictionary<string, IReadOnlyList<PontoXY>> { ["p1"] = QuadradoDe10Cm };

        var acao = () => ExportadorDeEncaixeParaPdf.Exportar(100, 100, posicoes, contornos);

        acao.Should().NotThrow();
    }

    [Fact]
    public void Exportar_PecaSemContornoConhecido_UsaRetanguloDeReserva()
    {
        var posicoes = new List<ItemDeResultado> { new("desconhecido#0", 0, 0, 10, 10, 0) };
        var contornos = new Dictionary<string, IReadOnlyList<PontoXY>>();

        var acao = () => ExportadorDeEncaixeParaPdf.Exportar(100, 100, posicoes, contornos);

        acao.Should().NotThrow();
    }

    [Fact]
    public void Exportar_SemBancada_UmaPaginaSo()
    {
        var posicoes = new List<ItemDeResultado> { new("p1#0", 0, 0, 10, 10, 0), new("p1#1", 0, 200, 10, 10, 0) };
        var contornos = new Dictionary<string, IReadOnlyList<PontoXY>> { ["p1"] = QuadradoDe10Cm };

        var bytes = ExportadorDeEncaixeParaPdf.Exportar(100, 210, posicoes, contornos);

        using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        doc.PageCount.Should().Be(1);
    }

    [Fact]
    public void Exportar_ComBancada_UmaPaginaPorBancadaOcupada()
    {
        // Bancada de 100cm: peça em y=0 fica na bancada 0, peça em y=150 fica na bancada 1.
        var posicoes = new List<ItemDeResultado> { new("p1#0", 0, 0, 10, 10, 0), new("p1#1", 0, 150, 10, 10, 0) };
        var contornos = new Dictionary<string, IReadOnlyList<PontoXY>> { ["p1"] = QuadradoDe10Cm };

        var bytes = ExportadorDeEncaixeParaPdf.Exportar(100, 200, posicoes, contornos, comprimentoBancadaCm: 100);

        using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        doc.PageCount.Should().Be(2);
    }

    [Fact]
    public void Exportar_ComBancadaMasSoUmaOcupada_UmaPaginaSo()
    {
        var posicoes = new List<ItemDeResultado> { new("p1#0", 0, 0, 10, 10, 0) };
        var contornos = new Dictionary<string, IReadOnlyList<PontoXY>> { ["p1"] = QuadradoDe10Cm };

        var bytes = ExportadorDeEncaixeParaPdf.Exportar(100, 100, posicoes, contornos, comprimentoBancadaCm: 100);

        using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        doc.PageCount.Should().Be(1);
    }
}
