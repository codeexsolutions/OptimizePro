using FluentAssertions;
using OptimizePro.Core.Arte;

namespace OptimizePro.Core.Tests.Arte;

public class ArteMoldeTests
{
    private static AjusteArte Ajuste(
        ModoEncaixeArte modo = ModoEncaixeArte.Caber, double escala = 100, double dx = 0, double dy = 0,
        int giro = 0, double? ppcm = null) =>
        new(TipoArte.Arte, modo, escala, dx, dy, giro, ppcm);

    [Fact]
    public void EncaixeDaArte_ModoCaber_EscolheMenorFatorESobraEspacoCentralizado()
    {
        var r = ArteMolde.EncaixeDaArte(100, 50, 200, 200, Ajuste(ModoEncaixeArte.Caber));

        // fator = min(200/100, 200/50) = min(2,4) = 2 -> 200x100
        r.Largura.Should().BeApproximately(200, 1e-9);
        r.Altura.Should().BeApproximately(100, 1e-9);
        r.X.Should().BeApproximately(0, 1e-9);
        r.Y.Should().BeApproximately(50, 1e-9); // (200-100)/2
    }

    [Fact]
    public void EncaixeDaArte_ModoCobrir_EscolheMaiorFatorETransbordaOAlvo()
    {
        var r = ArteMolde.EncaixeDaArte(100, 50, 200, 200, Ajuste(ModoEncaixeArte.Cobrir));

        // fator = max(2,4) = 4 -> 400x200
        r.Largura.Should().BeApproximately(400, 1e-9);
        r.Altura.Should().BeApproximately(200, 1e-9);
        r.X.Should().BeApproximately(-100, 1e-9); // (200-400)/2
        r.Y.Should().BeApproximately(0, 1e-9);
    }

    [Fact]
    public void EncaixeDaArte_ModoEsticar_IgnoraAspectoEPreencheOAlvoInteiro()
    {
        var r = ArteMolde.EncaixeDaArte(100, 50, 200, 300, Ajuste(ModoEncaixeArte.Esticar));

        r.Largura.Should().BeApproximately(200, 1e-9);
        r.Altura.Should().BeApproximately(300, 1e-9);
        r.X.Should().BeApproximately(0, 1e-9);
        r.Y.Should().BeApproximately(0, 1e-9);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(270)]
    public void EncaixeDaArte_GiroDe90Ou270_TrocaLarguraEAlturaAntesDoAjuste(int giro)
    {
        // arte 100x50 "deitada" vira efetivamente 50x100 antes do fator de "caber".
        var r = ArteMolde.EncaixeDaArte(100, 50, 200, 200, Ajuste(ModoEncaixeArte.Caber, giro: giro));

        // fator = min(200/50, 200/100) = min(4,2) = 2 -> 100x200
        r.Largura.Should().BeApproximately(100, 1e-9);
        r.Altura.Should().BeApproximately(200, 1e-9);
    }

    [Fact]
    public void EncaixeDaArte_GiroDe0Ou180_NaoTrocaDimensoes()
    {
        var r0 = ArteMolde.EncaixeDaArte(100, 50, 200, 200, Ajuste(ModoEncaixeArte.Caber, giro: 0));
        var r180 = ArteMolde.EncaixeDaArte(100, 50, 200, 200, Ajuste(ModoEncaixeArte.Caber, giro: 180));

        r0.Should().Be(r180);
        r0.Largura.Should().BeApproximately(200, 1e-9);
        r0.Altura.Should().BeApproximately(100, 1e-9);
    }

    [Theory]
    [InlineData(450, 90)]
    [InlineData(-90, 270)]
    [InlineData(-450, 270)]
    [InlineData(720, 0)]
    public void EncaixeDaArte_GiroForaDoIntervalo_NormalizaParaMultiploDe90EmModulo360(int giroBruto, int giroEsperadoEquivalente)
    {
        var comBruto = ArteMolde.EncaixeDaArte(100, 50, 200, 200, Ajuste(ModoEncaixeArte.Caber, giro: giroBruto));
        var comEsperado = ArteMolde.EncaixeDaArte(100, 50, 200, 200, Ajuste(ModoEncaixeArte.Caber, giro: giroEsperadoEquivalente));

        comBruto.Should().Be(comEsperado);
    }

    [Fact]
    public void EncaixeDaArte_Escala50PorCento_ReduzTamanhoMasMantemCentro()
    {
        var integral = ArteMolde.EncaixeDaArte(100, 50, 200, 200, Ajuste(ModoEncaixeArte.Caber, escala: 100));
        var metade = ArteMolde.EncaixeDaArte(100, 50, 200, 200, Ajuste(ModoEncaixeArte.Caber, escala: 50));

        metade.Largura.Should().BeApproximately(integral.Largura / 2, 1e-9);
        metade.Altura.Should().BeApproximately(integral.Altura / 2, 1e-9);
    }

    [Fact]
    public void EncaixeDaArte_Deslocamento_SomaAPosicaoCentralizada()
    {
        var semDeslocar = ArteMolde.EncaixeDaArte(100, 50, 200, 200, Ajuste(ModoEncaixeArte.Caber));
        var deslocado = ArteMolde.EncaixeDaArte(100, 50, 200, 200, Ajuste(ModoEncaixeArte.Caber, dx: 5, dy: -3));

        deslocado.X.Should().BeApproximately(semDeslocar.X + 5, 1e-9);
        deslocado.Y.Should().BeApproximately(semDeslocar.Y - 3, 1e-9);
        deslocado.Largura.Should().Be(semDeslocar.Largura);
        deslocado.Altura.Should().Be(semDeslocar.Altura);
    }

    [Fact]
    public void TamanhoDoRapport_ConverteDePixelsParaCmViaPpcm()
    {
        var t = ArteMolde.TamanhoDoRapport(500, 300, Ajuste(ppcm: 100));

        t.Largura.Should().BeApproximately(5.0, 1e-9);
        t.Altura.Should().BeApproximately(3.0, 1e-9);
    }

    [Fact]
    public void TamanhoDoRapport_ComGiro90_TrocaLarguraEAltura()
    {
        var t = ArteMolde.TamanhoDoRapport(500, 300, Ajuste(ppcm: 100, giro: 90));

        t.Largura.Should().BeApproximately(3.0, 1e-9);
        t.Altura.Should().BeApproximately(5.0, 1e-9);
    }

    [Fact]
    public void TamanhoDoRapport_AplicaEscala()
    {
        var t = ArteMolde.TamanhoDoRapport(500, 300, Ajuste(ppcm: 100, escala: 50));

        t.Largura.Should().BeApproximately(2.5, 1e-9);
        t.Altura.Should().BeApproximately(1.5, 1e-9);
    }

    [Fact]
    public void TamanhoDoRapport_SemPpcmArquivo_Lanca()
    {
        var acao = () => ArteMolde.TamanhoDoRapport(500, 300, Ajuste(ppcm: null));

        acao.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PpcmDaArte_PecaPequena_UsaODpiPedido()
    {
        // 10x10cm, dpi=300 -> ppcmPedido=300/2.54≈118.1; teto (26MP numa peça de 100cm²) é bem maior -> dpi vence.
        var ppcm = ArteMolde.PpcmDaArte(10, 10, 300);

        ppcm.Should().BeApproximately(300 / 2.54, 1e-6);
    }

    [Fact]
    public void PpcmDaArte_PecaEnorme_ForcaOTetoDeMegapixels()
    {
        // 1000x1000cm (10m x 10m) -> área 1_000_000cm² -> teto = sqrt(26_000_000/1_000_000) = sqrt(26).
        var ppcm = ArteMolde.PpcmDaArte(1000, 1000, 300);

        ppcm.Should().BeApproximately(Math.Sqrt(26), 1e-6);
    }

    [Fact]
    public void PpcmDaArte_DpiMuitoBaixo_RespeitaOMinimoDe4()
    {
        var ppcm = ArteMolde.PpcmDaArte(10, 10, 1);

        ppcm.Should().BeApproximately(4.0, 1e-9);
    }
}
