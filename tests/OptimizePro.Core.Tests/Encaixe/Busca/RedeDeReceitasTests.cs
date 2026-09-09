using System.Text.Json;
using FluentAssertions;
using OptimizePro.Core.Encaixe;
using OptimizePro.Core.Encaixe.Busca;

namespace OptimizePro.Core.Tests.Encaixe.Busca;

public class VetorizacaoDoTrabalhoTests
{
    [Fact]
    public void VetorDoTrabalho_TamanhoCorreto()
    {
        var pecas = new[] { new PecaParaRede(0.7, 20, 10, TipoDeGiro.MantemSentido) };

        var vetor = VetorizacaoDoTrabalho.VetorDoTrabalho(pecas, 150);

        vetor.Should().HaveCount(VetorizacaoDoTrabalho.Dimensao);
    }

    [Fact]
    public void VetorDoTrabalho_SemPecas_NaoLancaENaoTemNaN()
    {
        var vetor = VetorizacaoDoTrabalho.VetorDoTrabalho([], 150);

        vetor.Should().OnlyContain(v => !double.IsNaN(v));
    }

    [Fact]
    public void VetorDoTrabalho_ContaFracaoDeGiroLivreEFixaCorretamente()
    {
        PecaParaRede Peca(TipoDeGiro giro) => new(0.5, 10, 10, giro);

        var pecas = new[] { Peca(TipoDeGiro.Livre), Peca(TipoDeGiro.Livre), Peca(TipoDeGiro.Fixa), Peca(TipoDeGiro.MantemSentido) };

        var vetor = VetorizacaoDoTrabalho.VetorDoTrabalho(pecas, 150);

        vetor[10].Should().BeApproximately(2.0 / 4, 1e-9); // fração livres
        vetor[11].Should().BeApproximately(1.0 / 4, 1e-9); // fração fixas
    }

    [Fact]
    public void VetorDoTrabalho_LarguraDoTecidoAcimaDe600_SaturaEm2()
    {
        var pecas = new[] { new PecaParaRede(0.5, 10, 10, TipoDeGiro.MantemSentido) };

        var vetor = VetorizacaoDoTrabalho.VetorDoTrabalho(pecas, 900);

        vetor[1].Should().Be(2.0);
    }

    /// <summary>
    /// Regressão (02/09/2026) — achado comparando com <c>estatisticasPesadas</c> em
    /// <c>public/encaixe-rede.js</c>: a média de ocupação tem que ser PONDERADA pela
    /// quantidade de cada peça, não uma linha-um-voto. Um trabalho com 1 peça rara (ocupação
    /// baixa) e 199 cópias de outra (ocupação alta) tem que sair com a média bem perto da
    /// dominante — não meio a meio entre as duas linhas.
    /// </summary>
    [Fact]
    public void VetorDoTrabalho_MediaDeOcupacaoEhPonderadaPelaQuantidade_NaoPorLinhaDaTabela()
    {
        var pecas = new[]
        {
            new PecaParaRede(Ocupacao: 0.1, Largura: 10, Altura: 10, Giro: TipoDeGiro.MantemSentido, Quantidade: 1),
            new PecaParaRede(Ocupacao: 0.9, Largura: 10, Altura: 10, Giro: TipoDeGiro.MantemSentido, Quantidade: 199),
        };

        var vetor = VetorizacaoDoTrabalho.VetorDoTrabalho(pecas, 150);

        vetor[2].Should().BeGreaterThan(0.85); // média de ocupação (índice 2) — dominada pela peça de 199 cópias
    }

    [Fact]
    public void VetorDoTrabalho_FracaoDeGiroLivreEFixa_PonderaPelaQuantidade()
    {
        var pecas = new[]
        {
            new PecaParaRede(0.5, 10, 10, TipoDeGiro.Livre, Quantidade: 3),
            new PecaParaRede(0.5, 10, 10, TipoDeGiro.Fixa, Quantidade: 1),
        };

        var vetor = VetorizacaoDoTrabalho.VetorDoTrabalho(pecas, 150);

        vetor[10].Should().BeApproximately(3.0 / 4, 1e-9); // fração livres, por CÓPIA — não por linha (que daria 1/2)
        vetor[11].Should().BeApproximately(1.0 / 4, 1e-9);
    }
}

public class VocabularioDeReceitaTests
{
    [Fact]
    public void ChaveDaReceita_Contorno_MontaAsQuatroPartesCorretas()
    {
        var receita = Receita.DeContorno(AgrupamentoDeEncaixe.Dupla, CriterioDeOrdem.Area, HeuristicaDeContorno.Fundo);

        VocabularioDeReceita.ChaveDaReceita(receita).Should().Be("contorno/dupla/area/fundo");
    }

    [Fact]
    public void ChaveDaReceita_Retangulo_SempreMapeiaAgrupamentoParaEmpe()
    {
        var receita = Receita.DeRetangulo(CriterioDeOrdem.Largura, HeuristicaDeCaixa.Bssf);

        VocabularioDeReceita.ChaveDaReceita(receita).Should().Be("retangulo/empe/largura/bssf");
    }

    [Fact]
    public void VetorDaChave_TemDimensao22EExatamenteQuatroUns()
    {
        var vetor = VocabularioDeReceita.VetorDaChave("contorno/solta/area/fundo");

        vetor.Should().HaveCount(VocabularioDeReceita.Dimensao);
        vetor.Sum().Should().Be(4); // um "1" por campo (motor, agrupamento, ordem, heuristica)
    }

    [Fact]
    public void VetorDaChave_ChaveMalFormada_Lanca()
    {
        var acao = () => VocabularioDeReceita.VetorDaChave("contorno/solta/area");

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void VetorDaReceita_BateComVetorDaChaveDaMesmaReceita()
    {
        var receita = Receita.DeContorno(AgrupamentoDeEncaixe.Solta, CriterioDeOrdem.Lado, HeuristicaDeContorno.Vazio);

        VocabularioDeReceita.VetorDaReceita(receita).Should().Equal(VocabularioDeReceita.VetorDaChave("contorno/solta/lado/vazio"));
    }
}

public class RedeDeReceitasTests
{
    [Fact]
    public void CriarRede_FormatoDasCamadasBateComOsTamanhosPedidos()
    {
        var rede = RedeDeReceitas.CriarRede([34, 16, 8, 1], new Random(1));

        rede.Camadas.Should().HaveCount(3);
        rede.Camadas[0].Pesos.Should().HaveCount(16);
        rede.Camadas[0].Pesos[0].Should().HaveCount(34);
        rede.Camadas[1].Pesos.Should().HaveCount(8);
        rede.Camadas[2].Pesos.Should().HaveCount(1);
        rede.Camadas[2].Pesos[0].Should().HaveCount(8);
    }

    [Fact]
    public void Prever_RedeNovaEAleatoria_RetornaValorEntreZeroEUm()
    {
        var rede = RedeDeReceitas.CriarRede([RedeDeReceitas.DimensaoDeEntrada, 16, 8, 1], new Random(2));
        var entrada = new double[RedeDeReceitas.DimensaoDeEntrada];

        var previsao = RedeDeReceitas.Prever(rede, entrada);

        previsao.Should().BeInRange(0, 1);
    }

    [Fact]
    public void TreinarRede_PadraoTrivial_AprendeADistinguir()
    {
        // Um problema bem separável: entrada[0] > 0 => alvo 1, senão 0. Se o treino estiver
        // implementado certo (retropropagação de verdade, não um placebo), a rede converge.
        var rede = RedeDeReceitas.CriarRede([2, 4, 1], new Random(3));
        var aleatorio = new Random(4);

        var exemplos = Enumerable.Range(0, 40).Select(i =>
        {
            var x = (i % 2 == 0) ? 1.0 : -1.0;
            return new RedeDeReceitas.ExemploDeTreino([x, 0], x > 0 ? 1.0 : 0.0);
        }).ToList();

        RedeDeReceitas.TreinarRede(rede, exemplos, aleatorio, epocas: 200, taxa: 0.1);

        RedeDeReceitas.Prever(rede, [1.0, 0]).Should().BeGreaterThan(0.8);
        RedeDeReceitas.Prever(rede, [-1.0, 0]).Should().BeLessThan(0.2);
    }

    [Fact]
    public void PontuarReceitas_DedupChavesRepetidasEPontuaCadaUmaUmaVezSo()
    {
        var rede = RedeDeReceitas.CriarRede([RedeDeReceitas.DimensaoDeEntrada, 8, 1], new Random(5));
        var vetorTrabalho = new double[VetorizacaoDoTrabalho.Dimensao];

        var pontos = RedeDeReceitas.PontuarReceitas(rede, vetorTrabalho, ["contorno/solta/area/fundo", "contorno/solta/area/fundo", "retangulo/empe/area/bl"]);

        pontos.Should().HaveCount(2);
        pontos.Should().ContainKey("contorno/solta/area/fundo");
        pontos.Should().ContainKey("retangulo/empe/area/bl");
    }

    [Fact]
    public void RedeNeural_SerializacaoJson_RoundTripPreservaOsPesos()
    {
        var rede = RedeDeReceitas.CriarRede([RedeDeReceitas.DimensaoDeEntrada, 16, 8, 1], new Random(6));

        var json = JsonSerializer.Serialize(rede);
        var recarregada = JsonSerializer.Deserialize<RedeNeural>(json)!;

        var entrada = new double[RedeDeReceitas.DimensaoDeEntrada];
        RedeDeReceitas.Prever(recarregada, entrada).Should().Be(RedeDeReceitas.Prever(rede, entrada));
        recarregada.Tamanhos.Should().Equal(rede.Tamanhos);
    }
}
