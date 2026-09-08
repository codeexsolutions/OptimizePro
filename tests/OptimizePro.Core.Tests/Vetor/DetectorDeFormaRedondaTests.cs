using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Core.Tests.Vetor;

public class DetectorDeFormaRedondaTests
{
    private static List<PontoXY> PontosDoCirculo(double cx, double cy, double r, int n)
    {
        var pontos = new List<PontoXY>();
        for (var i = 0; i < n; i++)
        {
            var angulo = 2 * Math.PI * i / n;
            pontos.Add(new PontoXY(cx + r * Math.Cos(angulo), cy + r * Math.Sin(angulo)));
        }
        return pontos;
    }

    [Fact]
    public void Detectar_CirculoPerfeito_EhDetectadoComCentroERaioCorretos()
    {
        var pontos = PontosDoCirculo(0, 0, 5, 32);

        var resultado = DetectorDeFormaRedonda.Detectar(pontos, toleranciaDeErro: 0.01);

        resultado.Should().NotBeNull();
        resultado!.Value.Raio.Should().BeApproximately(5, 0.01);
    }

    [Fact]
    public void Detectar_Quadrado_NaoEhDetectadoMesmoComErroZeroNosCantos()
    {
        // Os 4 cantos de um quadrado ficam exatamente sobre o círculo circunscrito (erro
        // zero!) — só a peneira de área pega esse caso (área do quadrado bem menor que a
        // do círculo que passa pelos cantos).
        List<PontoXY> quadrado = [new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)];

        DetectorDeFormaRedonda.Detectar(quadrado, toleranciaDeErro: 0.01).Should().BeNull();
    }

    [Fact]
    public void Detectar_CirculoComUmPontoForaDaTolerancia_NaoEhDetectado()
    {
        var pontos = PontosDoCirculo(0, 0, 5, 32);
        pontos[0] = new PontoXY(pontos[0].X + 2, pontos[0].Y); // um ponto bem fora do círculo

        DetectorDeFormaRedonda.Detectar(pontos, toleranciaDeErro: 0.05).Should().BeNull();
    }

    [Fact]
    public void Detectar_ToleranciaDeAreaCustomizadaMaisFolgada_AceitaFormaLevementeOval()
    {
        // Não é bem um círculo (achatado), mas com tolerância de área bem alta passa.
        List<PontoXY> pontos = [];
        for (var i = 0; i < 24; i++)
        {
            var angulo = 2 * Math.PI * i / 24;
            pontos.Add(new PontoXY(5 * Math.Cos(angulo), 4.7 * Math.Sin(angulo)));
        }

        DetectorDeFormaRedonda.Detectar(pontos, toleranciaDeErro: 0.5, toleranciaDeArea: 0.5).Should().NotBeNull();
    }

    /// <summary>
    /// Regressão (02/09/2026) — achado com manchas circulares falsas aparecendo em formas
    /// PEQUENAS (letras, detalhes finos): um piso de tolerância absoluto (ex.: 0,8px, usado
    /// como mínimo em <c>VetorService</c>) é folgado demais pra um círculo de raio pequeno —
    /// 0,6px de erro num raio de 3px é 20% de erro relativo, bem mais que aceitável, mas
    /// passava porque 0,6 &lt; 0,8 (a tolerância absoluta usada isoladamente). Agora a
    /// tolerância EFETIVA também não pode passar de <see cref="DetectorDeFormaRedonda.ToleranciaDeErroRelativaAoRaio"/>
    /// do raio ajustado — o mesmo erro de 0,6px deve ser rejeitado nesse raio pequeno.
    /// </summary>
    [Fact]
    public void Detectar_FormaPequenaComErroAbsolutoDentroMasRelativoForaDoRaio_NaoEhDetectada()
    {
        var pontos = PontosDoCirculo(0, 0, 3, 16);
        pontos[0] = new PontoXY(pontos[0].X + 0.6, pontos[0].Y); // erro absoluto 0.6px, 20% do raio (3px)

        DetectorDeFormaRedonda.Detectar(pontos, toleranciaDeErro: 0.8).Should().BeNull();
    }
}
