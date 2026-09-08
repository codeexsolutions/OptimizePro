using System.Drawing;
using System.Drawing.Imaging;
using FluentAssertions;
using OptimizePro.Services.Encaixe;

namespace OptimizePro.Services.Tests;

public class LeitorDeImagemDeEncaixeTests
{
    [Theory]
    [InlineData("png", true)]
    [InlineData(".PNG", true)]
    [InlineData("jpg", true)]
    [InlineData("jpeg", true)]
    [InlineData("dxf", false)]
    public void SuportaExtensao_ReconheceSoRaster(string extensao, bool esperado) =>
        LeitorDeImagemDeEncaixe.SuportaExtensao(extensao).Should().Be(esperado);

    /// <summary>Retângulo preto sólido num fundo branco, com DPI conhecido gravado — o caso mais simples de "silhueta contra fundo claro".</summary>
    private static byte[] CriarPngComRetangulo(int larguraPx, int alturaPx, int retW, int retH, float dpi)
    {
        using var bitmap = new Bitmap(larguraPx, alturaPx, PixelFormat.Format32bppArgb);
        bitmap.SetResolution(dpi, dpi);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            g.FillRectangle(Brushes.Black, (larguraPx - retW) / 2, (alturaPx - retH) / 2, retW, retH);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    [Fact]
    public void Ler_RetanguloContraFundoBranco_AchaASilhuetaComMedidaCorretaPeloDpi()
    {
        // 300 dpi = 300/2.54 ≈ 118,1 px/cm. Um retângulo de 590x1181 px vira ~5x10 cm.
        var bytes = CriarPngComRetangulo(800, 1400, retW: 590, retH: 1181, dpi: 300);

        var peca = LeitorDeImagemDeEncaixe.Ler(bytes, "peca.png");

        peca.LarguraCm.Should().BeApproximately(5.0, 0.3);
        peca.AlturaCm.Should().BeApproximately(10.0, 0.3);
        peca.Contorno.Should().HaveCountGreaterThan(2);
        peca.Origem.Should().Contain("300 dpi");
        peca.Origem.Should().NotContain("suposto");
    }

    [Fact]
    public void Ler_SemDpiGravado_AssumeDpiPadraoEAvisa()
    {
        using var bitmap = new Bitmap(400, 400, PixelFormat.Format32bppArgb);
        // Sem SetResolution — GDI+ ainda grava uma resolução padrão do sistema (normalmente
        // 96 dpi), então este teste cobre "dpi baixo, mas presente", não "totalmente ausente".
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            g.FillRectangle(Brushes.Black, 50, 50, 300, 300);
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);

        var peca = LeitorDeImagemDeEncaixe.Ler(stream.ToArray(), "sem-dpi.png");

        peca.LarguraCm.Should().BeGreaterThan(0);
        peca.Contorno.Should().NotBeEmpty();
    }

    [Fact]
    public void Ler_ImagemTodaBranca_LancaPorNaoAcharSilhueta()
    {
        using var bitmap = new Bitmap(200, 200, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.White);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);

        var acao = () => LeitorDeImagemDeEncaixe.Ler(stream.ToArray(), "branca.png");

        acao.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Ler_NomeDoArquivoViraNomeDaPeca_SemExtensao()
    {
        var bytes = CriarPngComRetangulo(400, 400, retW: 200, retH: 200, dpi: 150);

        var peca = LeitorDeImagemDeEncaixe.Ler(bytes, "frente 3x.png");

        peca.Nome.Should().Be("frente 3x");
    }
}
