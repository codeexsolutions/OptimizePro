using System.Text;
using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Moldes;
using OptimizePro.Core.Moldes.Plt;

namespace OptimizePro.Core.Tests.Plt;

public class LeitorPltTests
{
    private static Stream ParaStream(string conteudo) => new MemoryStream(Encoding.UTF8.GetBytes(conteudo));

    [Theory]
    [InlineData("plt", true)]
    [InlineData(".PLT", true)]
    [InlineData("hpgl", true)]
    [InlineData("dxf", false)]
    public void SuportaExtensao_ReconhecePltEHpgl(string extensao, bool esperado)
    {
        new LeitorPlt().SuportaExtensao(extensao).Should().Be(esperado);
    }

    [Fact]
    public async Task LerAsync_QuadradoViaPuPdComLabel_CosturaENomeia()
    {
        const string plt = "PU0,0;PD10,0,10,10,0,10,0,0;PU5,5;LBP1\x03;";

        var resultado = await new LeitorPlt().LerAsync(ParaStream(plt), new OpcoesLeituraMolde("cm", ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].Nome.Should().Be("P1");
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(10.0, 1e-6);
        resultado.Pecas[0].AlturaCm.Should().BeApproximately(10.0, 1e-6);
    }

    [Fact]
    public async Task LerAsync_QuadradoComTrechoEmModoRelativoPr_FechaCorretamente()
    {
        // PA absoluto para os 2 primeiros pontos, depois PR (relativo) para o resto —
        // testa a troca de modo PA/PR no meio do desenho.
        const string plt = "PU0,0;PD10,0;PR;PD0,10,-10,0,0,-10;";

        var resultado = await new LeitorPlt().LerAsync(ParaStream(plt), new OpcoesLeituraMolde("cm", ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(10.0, 1e-6);
        resultado.Pecas[0].AlturaCm.Should().BeApproximately(10.0, 1e-6);
    }

    [Fact]
    public async Task LerAsync_CirculoViaCi_GeraPecaFechadaComDiametroCorreto()
    {
        const string plt = "PU0,0;CI5;";

        var resultado = await new LeitorPlt().LerAsync(ParaStream(plt), new OpcoesLeituraMolde("cm", ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(10.0, 0.1);
        resultado.Pecas[0].AlturaCm.Should().BeApproximately(10.0, 0.1);
    }

    [Fact]
    public async Task LerAsync_CirculoViaArcoAaCompletoAPartirDeUmPontoNaCircunferencia_FechaComDiametroCorreto()
    {
        // Pena desce em (5,0) e desenha um arco de 360° ao redor do centro (0,0):
        // deve fechar exatamente onde começou, formando um círculo de raio 5.
        const string plt = "PU5,0;PD;AA0,0,360;";

        var resultado = await new LeitorPlt().LerAsync(ParaStream(plt), new OpcoesLeituraMolde("cm", ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(10.0, 0.1);
        resultado.Pecas[0].AlturaCm.Should().BeApproximately(10.0, 0.1);
    }

    [Fact]
    public async Task LerAsync_ComandoScGeraAvisoMasNaoQuebraALeitura()
    {
        const string plt = "SC0,100,0,100;PU0,0;PD10,0,10,10,0,10,0,0;";

        var resultado = await new LeitorPlt().LerAsync(ParaStream(plt), new OpcoesLeituraMolde("cm", ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Avisos.Should().Contain(a => a.Contains("SC") && a.Contains("não é aplicado"));
        resultado.Pecas.Should().HaveCount(1);
    }

    [Fact]
    public async Task LerAsync_UnidadeForcadaMm_ConverteParaCm()
    {
        const string plt = "PU0,0;PD100,0,100,100,0,100,0,0;";

        var resultado = await new LeitorPlt().LerAsync(ParaStream(plt), new OpcoesLeituraMolde("mm", ModoLeituraVetor.Marcador));

        resultado.Unidade.Should().Be("mm");
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(10.0, 1e-6); // 100mm -> 10cm
    }

    [Fact]
    public async Task LerAsync_SemGeometria_RetornaErroDescritivo()
    {
        var resultado = await new LeitorPlt().LerAsync(ParaStream("IN;"), new OpcoesLeituraMolde(null, ModoLeituraVetor.Marcador));

        resultado.Erro.Should().NotBeNull();
        resultado.Pecas.Should().BeEmpty();
    }
}

/// <summary>
/// Testes de autoconsistência do decodificador PE: como não há arquivos PLT reais
/// disponíveis para validar contra o sistema atual, estes testes codificam números
/// com o MESMO algoritmo documentado (§8.1) e conferem que o decodificador os lê de
/// volta corretamente — não provam fidelidade ao formato real, só que a implementação
/// é internamente consistente com a especificação. Ver aviso em PltDecodificadorPe.
/// </summary>
public class PltDecodificadorPeTests
{
    private const int BaseContinuar = 63;
    private const int BaseTerminar = 191;

    private static string CodificarNumero(double valorReal, int bits = 6)
    {
        var magnitude = (long)Math.Round(Math.Abs(valorReal));
        var sinalBit = valorReal < 0 ? 1L : 0L;
        var bruto = (magnitude << 1) | sinalBit;
        var mascara = (1L << bits) - 1;

        var digitos = new List<int>();
        var resto = bruto;
        do
        {
            digitos.Add((int)(resto & mascara));
            resto >>= bits;
        } while (resto != 0);

        var sb = new StringBuilder();
        for (var i = 0; i < digitos.Count; i++)
        {
            var ultimo = i == digitos.Count - 1;
            var codigo = (ultimo ? BaseTerminar : BaseContinuar) + digitos[i];
            sb.Append((char)codigo);
        }
        return sb.ToString();
    }

    [Fact]
    public void Decodificar_ParDeltaRelativoPositivo_MovePosicaoCorretamente()
    {
        var desenhista = new PltDesenhista();
        desenhista.DefinirPena(true);

        var dados = CodificarNumero(30) + CodificarNumero(40);
        var avisos = new List<string>();

        PltDecodificadorPe.Decodificar(dados, desenhista, avisos);

        desenhista.Posicao.Should().Be(new PontoXY(30, 40));
        desenhista.Tracos.Should().BeEmpty(); // ainda em construção, não finalizado
        avisos.Should().BeEmpty();
    }

    [Fact]
    public void Decodificar_NumeroNegativo_PreservaSinal()
    {
        var desenhista = new PltDesenhista();
        desenhista.DefinirPena(true);

        var dados = CodificarNumero(-17) + CodificarNumero(5);
        PltDecodificadorPe.Decodificar(dados, desenhista, []);

        desenhista.Posicao.Should().Be(new PontoXY(-17, 5));
    }

    [Fact]
    public void Decodificar_PrefixoIgual_UsaCoordenadaAbsoluta()
    {
        var desenhista = new PltDesenhista();
        desenhista.DesenharSegmentoAbsoluto(new PontoXY(100, 100), false); // move sem desenhar
        desenhista.DefinirPena(true);

        var dados = "=" + CodificarNumero(30) + CodificarNumero(40);
        PltDecodificadorPe.Decodificar(dados, desenhista, []);

        desenhista.Posicao.Should().Be(new PontoXY(30, 40)); // absoluto, não soma aos 100,100
    }

    [Fact]
    public void Decodificar_PrefixoMenor_NaoDesenhaOPontoMasMoveAPosicao()
    {
        var desenhista = new PltDesenhista();
        desenhista.DefinirPena(true);

        var dados = "<" + CodificarNumero(30) + CodificarNumero(0);
        PltDecodificadorPe.Decodificar(dados, desenhista, []);

        desenhista.Posicao.Should().Be(new PontoXY(30, 0));
        desenhista.Tracos.Should().BeEmpty();
    }

    [Fact]
    public void Decodificar_DadosTruncados_GeraAvisoSemLancarExcecao()
    {
        var desenhista = new PltDesenhista();
        var avisos = new List<string>();

        PltDecodificadorPe.Decodificar("AB", desenhista, avisos); // sem terminador válido

        avisos.Should().NotBeEmpty();
    }
}
