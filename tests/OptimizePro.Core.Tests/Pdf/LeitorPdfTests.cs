using System.IO.Compression;
using System.Text;
using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Moldes;
using OptimizePro.Core.Moldes.Pdf;

namespace OptimizePro.Core.Tests.Pdf;

public class LeitorPdfTests
{
    private static Stream ParaStream(string conteudo) => new MemoryStream(Encoding.Latin1.GetBytes(conteudo));

    private static string ComprimirFlate(string conteudo)
    {
        using var destino = new MemoryStream();
        using (var zlib = new ZLibStream(destino, CompressionLevel.Optimal, leaveOpen: true))
        {
            var bytes = Encoding.Latin1.GetBytes(conteudo);
            zlib.Write(bytes, 0, bytes.Length);
        }
        return Encoding.Latin1.GetString(destino.ToArray());
    }

    [Theory]
    [InlineData("pdf", true)]
    [InlineData(".PDF", true)]
    [InlineData("svg", false)]
    public void SuportaExtensao_ReconheceApenasPdf(string extensao, bool esperado)
    {
        new LeitorPdf().SuportaExtensao(extensao).Should().Be(esperado);
    }

    [Fact]
    public async Task LerAsync_QuadradoViaContentStreamNaoComprimido_GeraPeca()
    {
        const string pdf = """
            3 0 obj
            << /Type /Page /Contents 4 0 R /Resources << >> >>
            endobj
            4 0 obj
            << /Length 40 >>
            stream
            0 0 m 100 0 l 100 100 l 0 100 l h S
            endstream
            endobj
            """;

        var resultado = await new LeitorPdf().LerAsync(ParaStream(pdf), new OpcoesLeituraMolde("cm", ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(100.0, 1e-6);
        resultado.Pecas[0].AlturaCm.Should().BeApproximately(100.0, 1e-6);
    }

    [Fact]
    public async Task LerAsync_ContentStreamComprimidoComFlateDecode_DescomprimeEDesenha()
    {
        var conteudoBruto = "0 0 m 50 0 l 50 50 l 0 50 l h S";
        var comprimido = ComprimirFlate(conteudoBruto);

        var pdf = $$"""
            3 0 obj
            << /Type /Page /Contents 4 0 R /Resources << >> >>
            endobj
            4 0 obj
            << /Filter /FlateDecode /Length 999 >>
            stream
            {{comprimido}}
            endstream
            endobj
            """;

        var resultado = await new LeitorPdf().LerAsync(ParaStream(pdf), new OpcoesLeituraMolde("cm", ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(50.0, 1e-6);
        resultado.Pecas[0].AlturaCm.Should().BeApproximately(50.0, 1e-6);
    }

    [Fact]
    public async Task LerAsync_TextoDentroDoContorno_Nomeia()
    {
        const string pdf = """
            3 0 obj
            << /Type /Page /Contents 4 0 R /Resources << >> >>
            endobj
            4 0 obj
            << /Length 200 >>
            stream
            0 0 m 100 0 l 100 100 l 0 100 l h S
            BT /F1 12 Tf 40 40 Td (P1) Tj ET
            endstream
            endobj
            """;

        var resultado = await new LeitorPdf().LerAsync(ParaStream(pdf), new OpcoesLeituraMolde("cm", ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].Nome.Should().Be("P1");
    }

    [Fact]
    public async Task LerAsync_XObjectFormComMatrizECm_AplicaTransformacoesEmCadeia()
    {
        const string pdf = """
            3 0 obj
            << /Type /Page /Contents 4 0 R /Resources << /XObject << /Fx 7 0 R >> >> >>
            endobj
            4 0 obj
            << /Length 60 >>
            stream
            q 1 0 0 1 50 50 cm /Fx Do Q
            endstream
            endobj
            7 0 obj
            << /Type /XObject /Subtype /Form /Matrix [2 0 0 2 0 0] /Length 60 >>
            stream
            0 0 m 10 0 l 10 10 l 0 10 l h S
            endstream
            endobj
            """;

        var resultado = await new LeitorPdf().LerAsync(ParaStream(pdf), new OpcoesLeituraMolde("cm", ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        // Form desenha 10x10 no seu próprio espaço; Matrix [2 0 0 2 0 0] escala por 2 -> 20x20.
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(20.0, 1e-6);
        resultado.Pecas[0].AlturaCm.Should().BeApproximately(20.0, 1e-6);
    }

    [Fact]
    public async Task LerAsync_PaginaDentroDeObjectStreamComprimido_EncontraEInterpreta()
    {
        // Objeto 3 (a página) só existe dentro do ObjStm — testa a expansão de ObjStm.
        const string corpoDaPagina = "<< /Type /Page /Contents 6 0 R /Resources << >> >>";
        var cabecalho = "3 0 "; // 1 par: objNum=3, offset=0 (relativo a /First)
        var decodificadoDoObjStm = cabecalho + corpoDaPagina;
        var comprimidoObjStm = ComprimirFlate(decodificadoDoObjStm);

        var pdf = $$"""
            5 0 obj
            << /Type /ObjStm /N 1 /First {{cabecalho.Length}} /Filter /FlateDecode /Length 999 >>
            stream
            {{comprimidoObjStm}}
            endstream
            endobj
            6 0 obj
            << /Length 40 >>
            stream
            0 0 m 30 0 l 30 30 l 0 30 l h S
            endstream
            endobj
            """;

        var resultado = await new LeitorPdf().LerAsync(ParaStream(pdf), new OpcoesLeituraMolde("cm", ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(30.0, 1e-6);
    }

    [Fact]
    public async Task LerAsync_ComEncrypt_RetornaErroDescritivo()
    {
        const string pdf = """
            1 0 obj
            << /Filter /Standard /V 1 /R 2 >>
            endobj
            2 0 obj
            << /Encrypt 1 0 R /Root 3 0 R >>
            endobj
            3 0 obj
            << /Type /Page /Contents 4 0 R /Resources << >> >>
            endobj
            4 0 obj
            << /Length 40 >>
            stream
            0 0 m 100 0 l 100 100 l 0 100 l h S
            endstream
            endobj
            """;

        var resultado = await new LeitorPdf().LerAsync(ParaStream(pdf), new OpcoesLeituraMolde(null, ModoLeituraVetor.Marcador));

        resultado.Erro.Should().NotBeNull();
        resultado.Erro.Should().Contain("senha");
    }

    [Fact]
    public async Task LerAsync_SemPaginas_RetornaErroDescritivo()
    {
        var resultado = await new LeitorPdf().LerAsync(ParaStream("1 0 obj\n<< /Foo /Bar >>\nendobj"), new OpcoesLeituraMolde(null, ModoLeituraVetor.Marcador));

        resultado.Erro.Should().NotBeNull();
        resultado.Pecas.Should().BeEmpty();
    }

    [Fact]
    public async Task LerAsync_UnidadePadraoEhPontos_ConverteParaCm()
    {
        // 72pt = 1 polegada = 2.54cm -> um quadrado de 72x72pt deve virar 2.54x2.54cm.
        const string pdf = """
            3 0 obj
            << /Type /Page /Contents 4 0 R /Resources << >> >>
            endobj
            4 0 obj
            << /Length 40 >>
            stream
            0 0 m 72 0 l 72 72 l 0 72 l h S
            endstream
            endobj
            """;

        var resultado = await new LeitorPdf().LerAsync(ParaStream(pdf), new OpcoesLeituraMolde(null, ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Unidade.Should().Be("pt");
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(2.54, 1e-6);
    }
}

public class PdfFiltrosTests
{
    [Fact]
    public void Decodificar_FlateComPreditorPngSubEUp_RestauraBytesOriginais()
    {
        // 2 linhas de 4 bytes (Colors=1, BitsPerComponent=8, Columns=4).
        // Linha0 crua: [10,20,30,40] filtrada com Sub (tipo 1).
        // Linha1 crua: [11,22,33,44] filtrada com Up (tipo 2), relativa à linha0 crua.
        byte[] linha0Filtrada = [1, 10, 10, 10, 10];
        byte[] linha1Filtrada = [2, 1, 2, 3, 4];
        var dadosFiltrados = linha0Filtrada.Concat(linha1Filtrada).ToArray();

        byte[] comprimido;
        using (var destino = new MemoryStream())
        {
            using (var zlib = new ZLibStream(destino, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(dadosFiltrados, 0, dadosFiltrados.Length);
            comprimido = destino.ToArray();
        }

        var dict = new Dictionary<string, object?>
        {
            ["Filter"] = "FlateDecode",
            ["DecodeParms"] = new Dictionary<string, object?>
            {
                ["Predictor"] = 15.0,
                ["Colors"] = 1.0,
                ["BitsPerComponent"] = 8.0,
                ["Columns"] = 4.0,
            },
        };

        var resultado = PdfFiltros.Decodificar(dict, comprimido);

        resultado.Should().Equal(10, 20, 30, 40, 11, 22, 33, 44);
    }

    [Fact]
    public void Decodificar_SemFiltro_DevolveBytesInalterados()
    {
        byte[] dados = [1, 2, 3];
        PdfFiltros.Decodificar([], dados).Should().Equal(dados);
    }
}

public class PdfAnalisadorDeObjetosTests
{
    [Fact]
    public void AnalisarValor_StringLiteralComEscapes_Decodifica()
    {
        var texto = @"(Ol\341, \(mundo\)\n)";
        var pos = 0;
        var resultado = PdfAnalisadorDeObjetos.AnalisarValor(texto, ref pos);

        resultado.Should().Be("Olá, (mundo)\n");
    }

    [Fact]
    public void AnalisarValor_StringHex_Decodifica()
    {
        var texto = "<50 31>"; // "P1" em hex, com espaço no meio (ignorado)
        var pos = 0;
        var resultado = PdfAnalisadorDeObjetos.AnalisarValor(texto, ref pos);

        resultado.Should().Be("P1");
    }

    [Fact]
    public void AnalisarValor_Referencia_ReconheceCorretamente()
    {
        var texto = "12 0 R";
        var pos = 0;
        var resultado = PdfAnalisadorDeObjetos.AnalisarValor(texto, ref pos);

        resultado.Should().Be(new PdfReferencia(12, 0));
    }

    [Fact]
    public void AnalisarValor_NumeroSimplesNaoViraReferencia()
    {
        var texto = "12.5";
        var pos = 0;
        var resultado = PdfAnalisadorDeObjetos.AnalisarValor(texto, ref pos);

        resultado.Should().Be(12.5);
    }

    [Fact]
    public void AnalisarValor_DicionarioAninhadoComArray_Analisa()
    {
        var texto = "<< /Type /Page /MediaBox [0 0 100 200] /Ref 5 0 R >>";
        var pos = 0;
        var resultado = PdfAnalisadorDeObjetos.AnalisarValor(texto, ref pos) as Dictionary<string, object?>;

        resultado.Should().NotBeNull();
        resultado!["Type"].Should().Be("Page");
        resultado["MediaBox"].Should().BeEquivalentTo(new List<object?> { 0.0, 0.0, 100.0, 200.0 });
        resultado["Ref"].Should().Be(new PdfReferencia(5, 0));
    }
}
