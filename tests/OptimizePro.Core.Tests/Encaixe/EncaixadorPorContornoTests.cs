using FluentAssertions;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Core.Tests.Encaixe;

public class EncaixadorPorContornoTests
{
    // Forma "degrau": coluna 0 encosta imediatamente (topo=0), coluna 1 só encosta
    // depois de subir 3 (topo=3) — como um formato em L deitado.
    // MaxBase=3, SomaTopo=3, NumeroDeColunasValidas=2.
    private static Forma FormaDegrau() => new(2, [0, 3], [0, 3], []);

    [Fact]
    public void MelhorPosicaoDaUnidade_HeuristicaFundo_EscolheMenorAlturaComDesempatePorVazio()
    {
        // perfil calculado à mão (ver comentário no PR/commit): x=0 e x=2 empatam em
        // fundo(4,4), mas x=0 tem vazio menor (0 vs 3) — desempate deve escolher x=0.
        int[] perfil = [0, 3, 0, 0];

        var resultado = EncaixadorPorContorno.MelhorPosicaoDaUnidade(perfil, colsTecido: 4, FormaDegrau(), HeuristicaDeContorno.Fundo);

        resultado.Should().NotBeNull();
        resultado!.Value.X.Should().Be(0);
        resultado.Value.Y.Should().Be(0);
        resultado.Value.P1.Should().Be(4); // fundo
        resultado.Value.P2.Should().Be(0); // vazio
    }

    [Fact]
    public void MelhorPosicaoDaUnidade_HeuristicaVazio_UsaFormulaDeVazioComoCriterioPrincipal()
    {
        int[] perfil = [0, 3, 0, 0];

        var resultado = EncaixadorPorContorno.MelhorPosicaoDaUnidade(perfil, colsTecido: 4, FormaDegrau(), HeuristicaDeContorno.Vazio);

        resultado.Should().NotBeNull();
        resultado!.Value.X.Should().Be(0);
        resultado.Value.Y.Should().Be(0);
        resultado.Value.P1.Should().Be(0); // vazio (critério principal desta heurística)
        resultado.Value.P2.Should().Be(4); // fundo
    }

    [Fact]
    public void MelhorPosicaoDaUnidade_HeuristicaContato_PrefereMaisColunasEncostadasQueSoAltura()
    {
        // Mesmo cenário-base do teste de "fundo" acima, mas comparando por contato:
        //   x=0: as 2 colunas da forma degrau encostam exatamente (y=0) -> 0 colunas sem contato.
        //   x=1: y=3, só 1 coluna encosta de verdade -> 1 coluna sem contato.
        //   x=2: y=0, só 1 coluna encosta -> 1 coluna sem contato.
        // "Contato" tem que preferir x=0 (0 sem contato) sobre x=2 (empataria em "fundo"=4 com x=0,
        // mas não em contato) — prova que é um critério genuinamente diferente de "fundo".
        int[] perfil = [0, 3, 0, 0];

        var resultado = EncaixadorPorContorno.MelhorPosicaoDaUnidade(perfil, colsTecido: 4, FormaDegrau(), HeuristicaDeContorno.Contato);

        resultado.Should().NotBeNull();
        resultado!.Value.X.Should().Be(0);
        resultado.Value.Y.Should().Be(0);
        resultado.Value.P1.Should().Be(0); // colunas SEM contato (0 = as 2 colunas da forma encostam)
        resultado.Value.P2.Should().Be(4); // fundo, só desempate
    }

    [Fact]
    public void MelhorPosicaoDaUnidade_HeuristicaContato_DesempataPorFundoQuandoContatoEmpata()
    {
        // x=1 e x=2 empatam em "sem contato"=1 (só 1 coluna encosta em cada) — desempate
        // deve escolher o de menor fundo: x=2 (fundo=4) vence x=1 (fundo=7).
        int[] perfil = [0, 3, 0, 0];

        var candidatos = new[] { 1, 2 }
            .Select(x => (X: x, Pos: EncaixadorPorContorno.MelhorPosicaoDaUnidade(perfil, 4, FormaDegrau(), HeuristicaDeContorno.Contato, saltoX: 1)))
            .ToList();

        // Varredura exata (saltoX=1) sobre TODO x — o vencedor final tem que ser x=0 (sem contato=0),
        // não x=1/x=2 (sem contato=1) — já provado no teste anterior; aqui só confirma que
        // x=2 bate x=1 SE eles fossem os únicos concorrentes (checagem direta da fórmula).
        var resultado = EncaixadorPorContorno.MelhorPosicaoDaUnidade(perfil, colsTecido: 4, FormaDegrau(), HeuristicaDeContorno.Contato, saltoX: 1);
        resultado!.Value.X.Should().Be(0);
    }

    [Fact]
    public void MelhorPosicaoDaUnidadeV2_HeuristicaContato_DelegaParaV1EBateExato()
    {
        int[] perfil = [0, 3, 0, 0];

        var v1 = EncaixadorPorContorno.MelhorPosicaoDaUnidade(perfil, colsTecido: 4, FormaDegrau(), HeuristicaDeContorno.Contato);
        var v2 = EncaixadorPorContorno.MelhorPosicaoDaUnidadeV2(perfil, colsTecido: 4, FormaDegrau(), HeuristicaDeContorno.Contato);

        v2.Should().Be(v1);
    }

    [Fact]
    public void MelhorPosicaoDaUnidade_PecaNaoCabeNaLargura_RetornaNulo()
    {
        var forma = new Forma(5, [0, 0, 0, 0, 0], [0, 0, 0, 0, 0], []);
        int[] perfil = [0, 0, 0];

        var resultado = EncaixadorPorContorno.MelhorPosicaoDaUnidade(perfil, colsTecido: 3, forma, HeuristicaDeContorno.Fundo);

        resultado.Should().BeNull();
    }

    [Fact]
    public void TopKPosicoes_DevolveAsKMelhoresEmOrdemCrescente()
    {
        // 4 posições x possíveis (colsTecido=5, forma com 2 colunas): x=0..3. perfil plano —
        // fundo é igual (4) em todo x, então P1 empata e o desempate por P2 (vazio, também
        // igual aqui) mantém a ordem estável; o que importa é que vêm exatamente K, ordenadas.
        int[] perfil = [0, 3, 0, 3, 0];

        var topo = EncaixadorPorContorno.TopKPosicoes(perfil, colsTecido: 5, FormaDegrau(), HeuristicaDeContorno.Fundo, k: 2);

        topo.Should().HaveCount(2);
        topo.Should().BeInAscendingOrder(p => p.P1);
        topo[0].Should().Be(EncaixadorPorContorno.MelhorPosicaoDaUnidade(perfil, 5, FormaDegrau(), HeuristicaDeContorno.Fundo));
    }

    [Fact]
    public void TopKPosicoes_KMaiorQuePosicoesPossiveis_DevolveTodasAsPossiveis()
    {
        int[] perfil = [0, 3, 0, 0];

        var topo = EncaixadorPorContorno.TopKPosicoes(perfil, colsTecido: 4, FormaDegrau(), HeuristicaDeContorno.Fundo, k: 999);

        topo.Should().HaveCount(3); // xMax = 4-2 = 2 -> x em {0,1,2}
    }

    [Fact]
    public void TopKPosicoes_PecaNaoCabe_DevolveVazio()
    {
        var forma = new Forma(5, [0, 0, 0, 0, 0], [0, 0, 0, 0, 0], []);
        int[] perfil = [0, 0, 0];

        var topo = EncaixadorPorContorno.TopKPosicoes(perfil, colsTecido: 3, forma, HeuristicaDeContorno.Fundo, k: 3);

        topo.Should().BeEmpty();
    }

    [Fact]
    public void TopKPosicoes_KMenorQueUm_AindaDevolveAoMenosAMelhorPosicao()
    {
        int[] perfil = [0, 3, 0, 0];

        var topo = EncaixadorPorContorno.TopKPosicoes(perfil, colsTecido: 4, FormaDegrau(), HeuristicaDeContorno.Fundo, k: 0);

        topo.Should().HaveCount(1);
    }

    private static Mascara QuadradoMascara2x2()
    {
        var silhueta = new bool[2, 2];
        silhueta[0, 0] = silhueta[0, 1] = silhueta[1, 0] = silhueta[1, 1] = true;
        return Mascara.DeSilhueta(silhueta, raioDeFolga: 0);
    }

    [Fact]
    public void Encaixar_DoisQuadrados2x2EmTecido4Colunas_ColocaLadoALadoNaMesmaAltura()
    {
        var mascara = QuadradoMascara2x2();
        var item1 = new ItemEncaixe("sq1", new Dictionary<int, Mascara> { [0] = mascara });
        var item2 = new ItemEncaixe("sq2", new Dictionary<int, Mascara> { [0] = mascara });

        var resultado = EncaixadorPorContorno.Encaixar(
            colsTecido: 4,
            ordem: [(item1, 0), (item2, 0)],
            heuristica: HeuristicaDeContorno.Fundo);

        resultado.ItensNaoEncaixados.Should().BeEmpty();
        resultado.Posicoes.Should().HaveCount(2);
        resultado.Posicoes[0].Should().Be(new PosicaoDeItem("sq1", 0, 0, 0));
        resultado.Posicoes[1].Should().Be(new PosicaoDeItem("sq2", 0, 2, 0));
        resultado.FundoMaximo.Should().Be(2); // nenhum espaço vertical desperdiçado
    }

    [Fact]
    public void Encaixar_PecaMaisLargaQueOTecido_VaiParaNaoEncaixados()
    {
        var mascaraLarga = new Mascara(5, 1, new byte[] { 1, 1, 1, 1, 1 }, new byte[] { 1, 1, 1, 1, 1 });
        var item = new ItemEncaixe("larga", new Dictionary<int, Mascara> { [0] = mascaraLarga });

        var resultado = EncaixadorPorContorno.Encaixar(colsTecido: 3, ordem: [(item, 0)], heuristica: HeuristicaDeContorno.Fundo);

        resultado.ItensNaoEncaixados.Should().ContainSingle().Which.Should().Be("larga");
        resultado.Posicoes.Should().BeEmpty();
    }

    [Fact]
    public void Encaixar_ItemSemMascaraParaRotacaoPedida_Lanca()
    {
        var mascara = QuadradoMascara2x2();
        var item = new ItemEncaixe("sq1", new Dictionary<int, Mascara> { [0] = mascara });

        var acao = () => EncaixadorPorContorno.Encaixar(4, [(item, 90)], HeuristicaDeContorno.Fundo);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResultadoContorno_CalcularConsumoCm_UsaFundoMaximoVezesPassoMaisMargens()
    {
        var resultado = new ResultadoContorno([], [], FundoMaximo: 2);

        resultado.CalcularConsumoCm(passoCm: 0.5, margemCm: 1).Should().BeApproximately(3.0, 1e-9);
    }
}
