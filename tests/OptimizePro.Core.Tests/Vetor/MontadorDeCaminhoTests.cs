using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Core.Tests.Vetor;

public class MontadorDeCaminhoTests
{
    private static ParametrosDeRemontagem Parametros() => new(ToleranciaDeReta: 0.1, ToleranciaDeArco: 0.1);

    [Fact]
    public void Montar_RetanguloComQuinaAcimaDe90Graus_ProduzQuatroRetas()
    {
        // Com "quina"=100°, o canto de 90° do retângulo é detectado como vivo (180-90=90 < 100).
        List<PontoXY> retangulo = [new(0, 0), new(10, 0), new(10, 5), new(0, 5), new(0, 0)];

        var caminho = MontadorDeCaminho.Montar(retangulo, quinaGraus: 100, Parametros());

        caminho.Inicio.Should().Be(new PontoXY(0, 0));
        caminho.Segmentos.Should().HaveCount(4);
        caminho.Segmentos.Should().AllSatisfy(s => s.Should().BeOfType<SegmentoReta>());
    }

    [Fact]
    public void Montar_RetanguloComQuinaPadrao_SuavizaOsCantosEmBezier()
    {
        // Com o "quina" padrão (55°), um canto de 90° NÃO é detectado como vivo
        // (180-90=90, que não fica abaixo de 55) — os 4 cantos saem suavizados.
        List<PontoXY> retangulo = [new(0, 0), new(10, 0), new(10, 5), new(0, 5), new(0, 0)];

        var caminho = MontadorDeCaminho.Montar(retangulo, quinaGraus: 55, Parametros());

        caminho.Segmentos.Should().HaveCount(4);
        caminho.Segmentos.Should().AllSatisfy(s => s.Should().BeOfType<SegmentoBezier>());
    }

    [Fact]
    public void Montar_Circulo_ProduzUmUnicoArcoComRaioCorreto()
    {
        List<PontoXY> circulo = [];
        const int n = 32;
        for (var i = 0; i < n; i++)
        {
            var ang = 2 * Math.PI * i / n;
            circulo.Add(new PontoXY(10 * Math.Cos(ang), 10 * Math.Sin(ang)));
        }

        var caminho = MontadorDeCaminho.Montar(circulo, quinaGraus: 55, Parametros());

        caminho.Segmentos.Should().ContainSingle();
        var arco = caminho.Segmentos[0].Should().BeOfType<SegmentoArco>().Subject;
        arco.Raio.Should().BeApproximately(10, 0.1);
        arco.GrandeArco.Should().BeTrue(); // varre a volta inteira, >180°
    }

    [Fact]
    public void Montar_TrianguloComQuinaAlta_ProduzTresRetas()
    {
        List<PontoXY> triangulo = [new(0, 0), new(10, 0), new(5, 10), new(0, 0)];

        var caminho = MontadorDeCaminho.Montar(triangulo, quinaGraus: 100, Parametros());

        caminho.Segmentos.Should().HaveCount(3);
        caminho.Segmentos.Should().AllSatisfy(s => s.Should().BeOfType<SegmentoReta>());
    }

    [Fact]
    public void Montar_PoucosPontos_NaoLancaEDevolveCaminhoTrivial()
    {
        MontadorDeCaminho.Montar([], 55, Parametros()).Segmentos.Should().BeEmpty();
        MontadorDeCaminho.Montar([new(0, 0)], 55, Parametros()).Segmentos.Should().BeEmpty();
    }

    /// <summary>
    /// Regressão (02/09/2026) — bug estrutural achado comparando com a referência: checar o
    /// ajuste de arco só contra os poucos vértices já simplificados é frágil demais (poucos
    /// pontos quaisquer tendem a admitir um círculo com erro bem baixo). Aqui, P0/P1/P2 (um
    /// triângulo raso, quase reto) admitem um círculo "quase perfeito" entre si — mas o
    /// contorno BRUTO de verdade entre P0 e P1 tem um espinho bem fora desse círculo (um
    /// recorte côncavo de verdade, tipo o garfo de um "Y", que Douglas-Peucker já tinha
    /// descartado por simplificação). Passando o bruto, o erro do espinho contra o círculo
    /// estoura a tolerância e o arco espúrio é corretamente rejeitado, caindo pra reta/Bézier.
    /// </summary>
    [Fact]
    public void Montar_EspinhoNoContornoBruto_DerrubaArcoQueOsPontosSimplificadosSozinhosAceitariam()
    {
        List<PontoXY> simplificado = [new(0, 0), new(10, 3), new(20, 0), new(0, 0)];
        var parametros = new ParametrosDeRemontagem(ToleranciaDeReta: 1.0, ToleranciaDeArco: 1.0);

        // Confirma a premissa: só com os 3 pontos simplificados, o ajuste de círculo tem erro
        // bem menor que a tolerância (é quase um ajuste exato) — combinação de pontos "boa
        // demais" pra revelar, sozinha, que há um recorte côncavo escondido entre eles.
        var circuloDosSimplificados = AjusteDeCirculo.Ajustar(simplificado[..3]);
        circuloDosSimplificados.Should().NotBeNull();
        simplificado[..3].Max(p => Math.Abs(Geometria.DistanciaEntre(p, circuloDosSimplificados!.Value.Centro) - circuloDosSimplificados.Value.Raio))
            .Should().BeLessThan(parametros.ToleranciaDeArco);

        // Mesma sequência simplificada, mas agora com o contorno BRUTO real por trás do trecho
        // P0->P1: em vez de ir direto, passa por um espinho bem fora do círculo ajustado.
        List<PontoXY> bruto = [new(0, 0), new(5, -20), new(10, 3), new(20, 0), new(0, 0)];
        List<int> indicesNoBruto = [0, 2, 3, 4];

        var comBruto = MontadorDeCaminho.Montar(simplificado, quinaGraus: 55, parametros, bruto, indicesNoBruto);
        comBruto.Segmentos.Should().NotContain(s => s is SegmentoArco, "o espinho no contorno bruto deveria estourar a tolerância do ajuste de círculo e derrubar o arco espúrio");
    }
}
