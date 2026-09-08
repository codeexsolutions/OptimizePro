using System.Drawing;
using System.Drawing.Imaging;
using FluentAssertions;
using OptimizePro.Services.Vetor;

namespace OptimizePro.Services.Tests;

public class VetorServiceTests
{
    private readonly VetorService _servico = new();

    private static byte[] GerarPng(int largura, int altura, Action<Graphics> desenhar)
    {
        using var bmp = new Bitmap(largura, altura, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            desenhar(g);
        }

        using var memoria = new MemoryStream();
        bmp.Save(memoria, ImageFormat.Png);
        return memoria.ToArray();
    }

    private static byte[] QuadradoVermelho(int largura = 40, int altura = 40) =>
        GerarPng(largura, altura, g => g.FillRectangle(Brushes.Red, 10, 10, 20, 20));

    private static byte[] CirculoPreto(int largura = 60, int altura = 60) =>
        GerarPng(largura, altura, g => g.FillEllipse(Brushes.Black, 5, 5, 50, 50));

    [Fact]
    public async Task VetorizarAsync_ImagemComForma_ProduzSvgComPath()
    {
        var resultado = await _servico.VetorizarAsync(QuadradoVermelho(), new OpcoesDeVetorizacao(Cores: 2, Detalhe: 1));

        resultado.Svg.Should().StartWith("<svg");
        resultado.Svg.Should().Contain("<path");
        resultado.LarguraPx.Should().Be(40);
        resultado.AlturaPx.Should().Be(40);
    }

    [Fact]
    public async Task VetorizarAsync_ImagemMaiorQue1800px_ReamostraParaOLadoMaiorMaximo()
    {
        var bytes = GerarPng(2000, 100, g => g.FillRectangle(Brushes.Blue, 0, 0, 2000, 100));

        var resultado = await _servico.VetorizarAsync(bytes, new OpcoesDeVetorizacao(Cores: 2));

        resultado.LarguraPx.Should().Be(1800);
        resultado.AlturaPx.Should().Be(90); // proporção mantida: 100 * (1800/2000)
    }

    [Fact]
    public async Task VetorizarAsync_UmaCor_ProduzUmaUnicaCamada()
    {
        var resultado = await _servico.VetorizarAsync(QuadradoVermelho(), new OpcoesDeVetorizacao(Cores: 1, Detalhe: 1));

        var quantidadeDePaths = resultado.Svg.Split("<path").Length - 1;
        quantidadeDePaths.Should().Be(1);
    }

    [Fact]
    public async Task VetorizarAsync_CirculoComRedondasAtivo_EmiteOCaminhoDeDoisArcos()
    {
        // Detecção de forma redonda (passo 5) sempre emite exatamente 2 arcos (semicírculos) —
        // ver VetorService.CaminhoDoCirculo. A camada de fundo (branco) pode ou não também
        // fechar num círculo dependendo da quantização; por isso ">= 2", não "== 2".
        var resultado = await _servico.VetorizarAsync(CirculoPreto(), new OpcoesDeVetorizacao(Cores: 2, Detalhe: 1, Redondas: true));

        var quantidadeDeArcos = resultado.Svg.Split('A').Length - 1;
        quantidadeDeArcos.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task VetorizarAsync_TokenJaCancelado_Lanca()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var acao = () => _servico.VetorizarAsync(QuadradoVermelho(), new OpcoesDeVetorizacao(), cts.Token);

        await acao.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task VetorizarAsync_ComAtalhoSilhueta_ProduzApenasUmaCor()
    {
        var resultado = await _servico.VetorizarAsync(QuadradoVermelho(), AtalhosDeVetorizacao.Silhueta);

        var quantidadeDePaths = resultado.Svg.Split("<path").Length - 1;
        quantidadeDePaths.Should().Be(1);
    }
}
