using System.Text;
using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Moldes;
using OptimizePro.Core.Moldes.Svg;

namespace OptimizePro.Core.Tests.Svg;

public class LeitorSvgTests
{
    private static Stream ParaStream(string conteudo) => new MemoryStream(Encoding.UTF8.GetBytes(conteudo));

    [Theory]
    [InlineData("svg", true)]
    [InlineData(".SVG", true)]
    [InlineData("dxf", false)]
    public void SuportaExtensao_ReconheceApenasSvg(string extensao, bool esperado)
    {
        new LeitorSvg().SuportaExtensao(extensao).Should().Be(esperado);
    }

    [Fact]
    public async Task LerAsync_RetSimples_GeraUmaPeca10x10()
    {
        const string svg = """<svg width="20cm" height="20cm"><rect x="0" y="0" width="10" height="10"/></svg>""";

        var resultado = await new LeitorSvg().LerAsync(ParaStream(svg), new OpcoesLeituraMolde(null, ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Unidade.Should().Be("cm");
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(10.0, 1e-6);
        resultado.Pecas[0].AlturaCm.Should().BeApproximately(10.0, 1e-6);
    }

    [Fact]
    public async Task LerAsync_PathComLinhasRetasETexto_FechaENomeia()
    {
        const string svg = """
            <svg width="20cm" height="20cm">
              <path d="M0,0 L10,0 L10,10 L0,10 Z"/>
              <text x="5" y="5">P1</text>
            </svg>
            """;

        var resultado = await new LeitorSvg().LerAsync(ParaStream(svg), new OpcoesLeituraMolde(null, ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].Nome.Should().Be("P1");
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(10.0, 1e-6);
        resultado.Pecas[0].AlturaCm.Should().BeApproximately(10.0, 1e-6);
    }

    [Fact]
    public async Task LerAsync_PathComDoisArcosFormandoCirculo_DiametroCorreto()
    {
        // Dois semicírculos (comando A) fechando um círculo de raio 10 centrado na origem.
        const string svg = """<svg><path d="M10,0 A10,10 0 0 1 -10,0 A10,10 0 0 1 10,0 Z"/></svg>""";

        var resultado = await new LeitorSvg().LerAsync(ParaStream(svg), new OpcoesLeituraMolde("cm", ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(20.0, 0.2);
        resultado.Pecas[0].AlturaCm.Should().BeApproximately(20.0, 0.2);
    }

    [Fact]
    public async Task LerAsync_RetDentroDeGComScale_AplicaTransformacao()
    {
        const string svg = """<svg width="20cm" height="20cm"><g transform="scale(2)"><rect x="0" y="0" width="5" height="5"/></g></svg>""";

        var resultado = await new LeitorSvg().LerAsync(ParaStream(svg), new OpcoesLeituraMolde(null, ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(10.0, 1e-6); // 5 * scale(2) = 10
        resultado.Pecas[0].AlturaCm.Should().BeApproximately(10.0, 1e-6);
    }

    [Fact]
    public async Task LerAsync_UseReferenciaRetDentroDeDefs_ResolveEIgnoraDefsDireto()
    {
        const string svg = """
            <svg width="30cm" height="30cm">
              <defs><rect id="q" x="0" y="0" width="10" height="10"/></defs>
              <use href="#q" x="5" y="5"/>
            </svg>
            """;

        var resultado = await new LeitorSvg().LerAsync(ParaStream(svg), new OpcoesLeituraMolde(null, ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Avisos.Should().NotContain(a => a.Contains("não encontrado"));
        // Se <defs> não fosse ignorado como alvo direto, o rect apareceria 2x (original + via <use>).
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(10.0, 1e-6);
    }

    [Fact]
    public async Task LerAsync_UseReferenciaIdInexistente_GeraAvisoSemQuebrar()
    {
        const string svg = """<svg width="20cm" height="20cm"><use href="#naoexiste" /><rect x="0" y="0" width="10" height="10"/></svg>""";

        var resultado = await new LeitorSvg().LerAsync(ParaStream(svg), new OpcoesLeituraMolde(null, ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Avisos.Should().Contain(a => a.Contains("não encontrado"));
        resultado.Pecas.Should().HaveCount(1);
    }

    [Fact]
    public async Task LerAsync_ComViewBoxEscalandoParaCm_ConverteCorretamente()
    {
        const string svg = """<svg width="10cm" height="10cm" viewBox="0 0 100 100"><rect x="0" y="0" width="50" height="50"/></svg>""";

        var resultado = await new LeitorSvg().LerAsync(ParaStream(svg), new OpcoesLeituraMolde(null, ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(5.0, 1e-6); // metade do viewBox -> metade dos 10cm
        resultado.Pecas[0].AlturaCm.Should().BeApproximately(5.0, 1e-6);
    }

    [Fact]
    public async Task LerAsync_LarguraSemUnidadeExplicita_AssumePxEAvisa()
    {
        const string svg = """<svg width="200" height="200"><rect x="0" y="0" width="100" height="100"/></svg>""";

        var resultado = await new LeitorSvg().LerAsync(ParaStream(svg), new OpcoesLeituraMolde(null, ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Avisos.Should().Contain(a => a.Contains("sem unidade explícita"));
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(100.0 * 2.54 / 96.0, 1e-6);
    }

    [Fact]
    public async Task LerAsync_Circulo_DiametroCorreto()
    {
        const string svg = """<svg><circle cx="0" cy="0" r="5"/></svg>""";

        var resultado = await new LeitorSvg().LerAsync(ParaStream(svg), new OpcoesLeituraMolde("cm", ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(10.0, 0.1);
        resultado.Pecas[0].AlturaCm.Should().BeApproximately(10.0, 0.1);
    }

    [Fact]
    public async Task LerAsync_Poligono_FechaAutomaticamente()
    {
        const string svg = """<svg><polygon points="0,0 10,0 10,10 0,10"/></svg>""";

        var resultado = await new LeitorSvg().LerAsync(ParaStream(svg), new OpcoesLeituraMolde("cm", ModoLeituraVetor.Marcador));

        resultado.Erro.Should().BeNull();
        resultado.Pecas.Should().HaveCount(1);
        resultado.Pecas[0].LarguraCm.Should().BeApproximately(10.0, 1e-6);
        resultado.Pecas[0].AlturaCm.Should().BeApproximately(10.0, 1e-6);
    }

    [Fact]
    public async Task LerAsync_SemGeometria_RetornaErroDescritivo()
    {
        var resultado = await new LeitorSvg().LerAsync(ParaStream("<svg></svg>"), new OpcoesLeituraMolde(null, ModoLeituraVetor.Marcador));

        resultado.Erro.Should().NotBeNull();
        resultado.Pecas.Should().BeEmpty();
    }

    [Fact]
    public async Task LerAsync_XmlInvalido_RetornaErroSemLancarExcecao()
    {
        var resultado = await new LeitorSvg().LerAsync(ParaStream("<svg><rect></svg>"), new OpcoesLeituraMolde(null, ModoLeituraVetor.Marcador));

        resultado.Erro.Should().NotBeNull();
    }
}
