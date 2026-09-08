using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Encaixe;
using OptimizePro.Services.Encaixe;

namespace OptimizePro.Services.Tests;

public class ValidadorDeSobreposicaoNfpTests
{
    private static readonly Grade GradeFina = Grade.Calcular(larguraTecidoCm: 100, espacoCm: 0);

    private static IReadOnlyList<PontoXY> Retangulo(double largura, double altura) =>
        [new(0, 0), new(largura, 0), new(largura, altura), new(0, altura)];

    [Fact]
    public void SemSobreposicao_NenhumaPeca_DevolveVerdadeiro() =>
        ValidadorDeSobreposicaoNfp.SemSobreposicao([], GradeFina).Should().BeTrue();

    [Fact]
    public void SemSobreposicao_UmaSoPeca_DevolveVerdadeiro()
    {
        var posicionados = new[] { (Retangulo(10, 10), 0.0, 0.0) };

        ValidadorDeSobreposicaoNfp.SemSobreposicao(posicionados, GradeFina).Should().BeTrue();
    }

    [Fact]
    public void SemSobreposicao_DuasPecasLadoALadoSemInvadir_DevolveVerdadeiro()
    {
        var posicionados = new[]
        {
            (Retangulo(10, 10), 0.0, 0.0),
            (Retangulo(10, 10), 10.0, 0.0), // encostada exatamente na borda direita da primeira
        };

        ValidadorDeSobreposicaoNfp.SemSobreposicao(posicionados, GradeFina).Should().BeTrue();
    }

    [Fact]
    public void SemSobreposicao_DuasPecasInvadindoUmaAOutra_DevolveFalso()
    {
        var posicionados = new[]
        {
            (Retangulo(10, 10), 0.0, 0.0),
            (Retangulo(10, 10), 5.0, 5.0), // desloca só metade — invade o quadrante central
        };

        ValidadorDeSobreposicaoNfp.SemSobreposicao(posicionados, GradeFina).Should().BeFalse();
    }

    [Fact]
    public void SemSobreposicao_DuasPecasEmpilhadasVerticalmenteSemInvadir_DevolveVerdadeiro()
    {
        var posicionados = new[]
        {
            (Retangulo(10, 10), 0.0, 0.0),
            (Retangulo(10, 10), 0.0, 10.0),
        };

        ValidadorDeSobreposicaoNfp.SemSobreposicao(posicionados, GradeFina).Should().BeTrue();
    }

    [Fact]
    public void SemSobreposicao_TresPecasComUmaSoInvasaoNoMeio_DevolveFalso()
    {
        var posicionados = new[]
        {
            (Retangulo(10, 10), 0.0, 0.0),
            (Retangulo(10, 10), 20.0, 0.0), // longe, sem contato
            (Retangulo(10, 10), 2.0, 2.0),  // invade a primeira
        };

        ValidadorDeSobreposicaoNfp.SemSobreposicao(posicionados, GradeFina).Should().BeFalse();
    }

    [Fact]
    public void SemSobreposicao_FormaComConcavidadeMaisPecaQuePreencheORecorte_DevolveVerdadeiro()
    {
        // "L" 10×10 com recorte 5×5 no canto superior direito + um quadrado 5×5 posicionado
        // exatamente nesse recorte — juntas tiIam um quadrado 10×10 cheio, sem sobra nem
        // sobreposição nenhuma. Prova que o validador não acusa falso positivo em formas
        // côncavas que se tocam pela borda do recorte.
        IReadOnlyList<PontoXY> formaEmL = [new(0, 0), new(10, 0), new(10, 5), new(5, 5), new(5, 10), new(0, 10)];
        IReadOnlyList<PontoXY> quadradoPreenchedor = Retangulo(5, 5);

        var posicionados = new[]
        {
            (formaEmL, 0.0, 0.0),
            (quadradoPreenchedor, 5.0, 5.0), // ocupa exatamente o quadrante que falta na L
        };

        ValidadorDeSobreposicaoNfp.SemSobreposicao(posicionados, GradeFina).Should().BeTrue();
    }

    [Fact]
    public void SemSobreposicao_QuadradoPreenchedorInvadindoAForma_DevolveFalso()
    {
        // Mesma dupla de peças do teste anterior, mas o quadrado entra um pouco mais pra
        // dentro (4,4 em vez de 5,5) — invade o "L" de verdade.
        IReadOnlyList<PontoXY> formaEmL = [new(0, 0), new(10, 0), new(10, 5), new(5, 5), new(5, 10), new(0, 10)];
        IReadOnlyList<PontoXY> quadradoPreenchedor = Retangulo(5, 5);

        var posicionados = new[]
        {
            (formaEmL, 0.0, 0.0),
            (quadradoPreenchedor, 4.0, 4.0),
        };

        ValidadorDeSobreposicaoNfp.SemSobreposicao(posicionados, GradeFina).Should().BeFalse();
    }
}
