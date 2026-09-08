using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Encaixe;
using OptimizePro.Data.Repositorios;
using OptimizePro.Services.Encaixe;

namespace OptimizePro.Services.Tests;

public class EncaixeServiceTests : IDisposable
{
    private readonly BancoDeTeste _banco = new();
    private readonly EncaixeMemoriaService _memoria;
    private readonly EncaixeService _servico;

    public EncaixeServiceTests()
    {
        var repositorio = new EncaixeMemoriaRepository(_banco.Db);
        _memoria = new EncaixeMemoriaService(repositorio);
        _servico = new EncaixeService(_memoria);
    }

    public void Dispose() => _banco.Dispose();

    private static ConfiguracaoDeEncaixe ConfigRapida(double larguraTecido = 150, double espaco = 0.5, double margem = 1) =>
        new(larguraTecido, espaco, margem, TempoMaximoMs: 150, MsSemGanhoParaParedeMs: 50);

    private static PecaParaEncaixar Retangulo(string id, double largura, double altura, int quantidade = 1, TipoDeGiro giro = TipoDeGiro.MantemSentido) =>
        new(id, [new PontoXY(0, 0), new PontoXY(largura, 0), new PontoXY(largura, altura), new PontoXY(0, altura)], quantidade, giro);

    [Fact]
    public async Task BuscarMelhorEncaixe_PecasQueCabem_EncaixaTodasSemPerdas()
    {
        var pecas = new[] { Retangulo("A", 20, 10, quantidade: 3), Retangulo("B", 15, 15, quantidade: 2) };

        var resultado = await _servico.BuscarMelhorEncaixeAsync(pecas, ConfigRapida());

        resultado.ItensNaoEncaixados.Should().BeEmpty();
        resultado.Posicoes.Should().HaveCount(5); // 3 de A + 2 de B
        resultado.ConsumoCm.Should().BeGreaterThan(0);
        resultado.Tentativas.Should().BeGreaterThan(0);
        resultado.AproveitamentoPercentual.Should().BeInRange(0, 100);
        resultado.ReceitaVencedora.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task BuscarMelhorEncaixe_RegistraABuscaNaMemoriaAutomaticamente()
    {
        var pecas = new[] { Retangulo("A", 20, 10, quantidade: 3), Retangulo("B", 15, 15, quantidade: 2) };

        await _servico.BuscarMelhorEncaixeAsync(pecas, ConfigRapida());
        await _servico.BuscarMelhorEncaixeAsync(pecas, ConfigRapida());

        // mesmas peças/largura => mesma assinatura (§11.12) => as duas buscas contam pro mesmo tipo.
        var memoria = await _memoria.ConsultarMemoriaAsync(AssinaturaDoTrabalho(pecas, 150));
        memoria.EncaixesDoTipo.Should().Be(2);
        memoria.EncaixesNoTotal.Should().Be(2);
        memoria.Receitas.Should().NotBeEmpty();
    }

    private static string AssinaturaDoTrabalho(IReadOnlyList<PecaParaEncaixar> pecas, double larguraTecido)
    {
        var pecasParaRede = pecas.Select(p =>
        {
            var caixa = OptimizePro.Core.Geometria.CaixaDeContorno(p.Contorno);
            var areaReal = Math.Abs(OptimizePro.Core.Geometria.AreaComSinal(p.Contorno));
            var ocupacao = caixa.Largura * caixa.Altura > 0 ? areaReal / (caixa.Largura * caixa.Altura) : 1.0;
            return new OptimizePro.Core.Encaixe.Busca.PecaParaRede(ocupacao, caixa.Largura, caixa.Altura, p.Giro);
        }).ToList();

        return OptimizePro.Core.Encaixe.Busca.AssinaturaDeTrabalho.Calcular(pecasParaRede, larguraTecido);
    }

    /// <summary>
    /// Um "L": retângulo 10×10 com um recorte de 5×5 no canto — a cópia dela invertida 180°
    /// fecha o vão exatamente, tilando um quadrado 10×10 cheio (§11.8, "manga com manga
    /// invertida fecha quase um retângulo"). Peça clássica pra provar bloco vencendo solta:
    /// sozinha ocupa 10×10 (75% de aproveitamento real); em dupla, duas cabem no mesmo 10×10.
    /// </summary>
    private static PecaParaEncaixar PecaEmL(string id, int quantidade, TipoDeGiro giro = TipoDeGiro.MantemSentido) =>
        new(id, [new PontoXY(0, 0), new PontoXY(10, 0), new PontoXY(10, 5), new PontoXY(5, 5), new PontoXY(5, 10), new PontoXY(0, 10)], quantidade, giro);

    [Fact]
    public async Task BuscarMelhorEncaixe_PecaEmLComVariasCopias_AproveitaBemMelhorQueOPiorCasoSemNenhumEncaixeEntreElas()
    {
        // Tecido estreito o bastante (20cm — cabem só 2 "L" de 10cm por fileira) pra forçar
        // empilhar, onde interligar as peças realmente faz diferença. Pior caso possível (cada
        // cópia isolada, sem nenhum contato): 8×10cm = 80cm de fundo. Não afirmo qual receita
        // vence (o encaixe guloso comum já interliga formas côncavas sozinho, então "dupla"
        // vencer por nome não é garantido) — só que o resultado tem que ficar bem longe do
        // pior caso, com dupla/trio (§11.8) agora disputando de verdade sem travar.
        var pecas = new[] { PecaEmL("l", quantidade: 8) };
        var config = new ConfiguracaoDeEncaixe(20, 0, 1, TempoMaximoMs: 4_000, MsSemGanhoParaParedeMs: 1_000);

        var resultado = await _servico.BuscarMelhorEncaixeAsync(pecas, config);

        resultado.ItensNaoEncaixados.Should().BeEmpty();
        resultado.ConsumoCm.Should().BeLessThan(70);
    }

    /// <summary>Um quadrado 5×5 — cabe exatamente no recorte do <see cref="PecaEmL"/>, formato DIFERENTE dela (pra exercitar "cruzada", não dupla/trio).</summary>
    private static PecaParaEncaixar PecaQuadradaPreenchedora(string id, int quantidade, TipoDeGiro giro = TipoDeGiro.MantemSentido) =>
        new(id, [new PontoXY(0, 0), new PontoXY(5, 0), new PontoXY(5, 5), new PontoXY(0, 5)], quantidade, giro);

    [Fact]
    public async Task BuscarMelhorEncaixe_DoisFormatosQueEncaixamEntreSi_NaoTravaComCruzadaNaDisputa()
    {
        // "L" grande com recorte 5×5 + quadrado 5×5 que cabe exatamente nesse recorte —
        // formatos DIFERENTES, o caso clássico de "cruzada" (§11.8): a peça pequena no vão da
        // grande. Tecido estreito o bastante pra empilhar importar de verdade.
        var pecas = new[] { PecaEmL("l", quantidade: 4), PecaQuadradaPreenchedora("q", quantidade: 4) };
        var config = new ConfiguracaoDeEncaixe(20, 0, 1, TempoMaximoMs: 4_000, MsSemGanhoParaParedeMs: 1_000);

        var resultado = await _servico.BuscarMelhorEncaixeAsync(pecas, config);

        resultado.ItensNaoEncaixados.Should().BeEmpty();
        resultado.Posicoes.Should().HaveCount(8);
        // Sem nenhum contato entre formatos: 4 "L" empilhados (40cm) + 4 quadrados de 5cm
        // empilhados numa segunda coluna (20cm) — mas dá pra empilhar tudo numa coluna só
        // porque cabem 2 por fileira; o teto abaixo é generoso de propósito (sem exigir que
        // cruzada vença), só pra garantir que não regrediu nem travou.
        resultado.ConsumoCm.Should().BeLessThan(70);
    }

    [Fact]
    public async Task BuscarMelhorEncaixe_PecaEmLComGiroFixa_NaoTravaMesmoComReceitasDeBlocoNaDisputa()
    {
        // Sem giro 180°, a peça nunca pode formar bloco de verdade (§11.8 — o bloco depende da
        // cópia invertida) — mas a receita "dupla"/"trio" ainda entra na disputa e pode até
        // vencer (ela só degrada pra colocar cada cópia solta, o que é válido, não um erro).
        // O que este teste garante é que isso não trava nem lança: cada unidade cai no caminho
        // solta corretamente quando o bloco não é viável.
        var pecas = new[] { PecaEmL("l", quantidade: 4, giro: TipoDeGiro.Fixa) };
        var config = new ConfiguracaoDeEncaixe(150, 0, 1, TempoMaximoMs: 1_000, MsSemGanhoParaParedeMs: 300);

        var resultado = await _servico.BuscarMelhorEncaixeAsync(pecas, config);

        resultado.ItensNaoEncaixados.Should().BeEmpty();
        resultado.Posicoes.Should().HaveCount(4);
        resultado.Posicoes.Should().OnlyContain(p => p.RotacaoGraus == 0);
    }

    [Fact]
    public async Task BuscarMelhorEncaixe_PecaEmLComUmaSoCopia_NuncaFormaBloco()
    {
        // Uma cópia sozinha nunca tem parceira — precisa continuar encaixando normalmente,
        // não travar nem lançar, mesmo com receitas "dupla"/"trio" na disputa.
        var pecas = new[] { PecaEmL("l", quantidade: 1) };
        var config = ConfigRapida();

        var resultado = await _servico.BuscarMelhorEncaixeAsync(pecas, config);

        resultado.ItensNaoEncaixados.Should().BeEmpty();
        resultado.Posicoes.Should().HaveCount(1);
    }

    [Fact]
    public async Task BuscarMelhorEncaixe_PecaMaisLargaQueOTecido_VaiParaNaoEncaixados()
    {
        var pecas = new[] { Retangulo("A", 10, 10), Retangulo("MuitoLarga", 500, 10) };

        var resultado = await _servico.BuscarMelhorEncaixeAsync(pecas, ConfigRapida(larguraTecido: 100));

        resultado.ItensNaoEncaixados.Should().Contain(id => id.StartsWith("MuitoLarga"));
        resultado.Posicoes.Should().Contain(p => p.PecaId.StartsWith("A"));
    }

    [Fact]
    public async Task BuscarMelhorEncaixe_PecasIguaisQuantidadeMaiorQueUm_ExpandeEmItensIndividuais()
    {
        var pecas = new[] { Retangulo("A", 10, 10, quantidade: 4) };

        var resultado = await _servico.BuscarMelhorEncaixeAsync(pecas, ConfigRapida());

        resultado.Posicoes.Should().HaveCount(4);
        resultado.Posicoes.Select(p => p.PecaId).Should().OnlyHaveUniqueItems();
        resultado.Posicoes.Should().OnlyContain(p => p.PecaId.StartsWith("A#"));
    }

    [Fact]
    public async Task BuscarMelhorEncaixe_SemPecas_LancaArgumentException()
    {
        var acao = () => _servico.BuscarMelhorEncaixeAsync([], ConfigRapida());
        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task BuscarMelhorEncaixe_TodasAsQuantidadesZero_LancaArgumentException()
    {
        var acao = () => _servico.BuscarMelhorEncaixeAsync([Retangulo("A", 10, 10, quantidade: 0)], ConfigRapida());
        await acao.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// <c>System.Progress&lt;T&gt;</c> reposta via <c>SynchronizationContext.Post</c> — sem um
    /// contexto capturado (caso de teste), isso vira <c>ThreadPool.QueueUserWorkItem</c> por
    /// chamada, que NÃO preserva a ordem entre chamadas concorrentes de fatias diferentes,
    /// mesmo o serviço serializando os <c>Report</c> com lock. Reportando síncrono aqui
    /// (dentro do próprio lock do serviço) preserva a ordem de verdade.
    /// </summary>
    private sealed class ProgressoSincrono<T>(Action<T> relatar) : IProgress<T>
    {
        public void Report(T value) => relatar(value);
    }

    [Fact]
    public async Task BuscarMelhorEncaixe_ReportaProgressoComTentativasCrescentes()
    {
        var relatos = new List<AndamentoDoEncaixe>();
        var progresso = new ProgressoSincrono<AndamentoDoEncaixe>(a => relatos.Add(a));

        var pecas = new[] { Retangulo("A", 20, 10, quantidade: 3), Retangulo("B", 15, 15, quantidade: 2) };
        await _servico.BuscarMelhorEncaixeAsync(pecas, ConfigRapida(), progresso);

        relatos.Should().NotBeEmpty();
        relatos.Select(r => r.Tentativas).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task BuscarMelhorEncaixe_TokenJaCancelado_Lanca()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var acao = () => _servico.BuscarMelhorEncaixeAsync([Retangulo("A", 10, 10)], ConfigRapida(), cancelamento: cts.Token);

        // Task.Run com um token já cancelado nem chega a invocar o delegate — a fatia inteira
        // cancela antes da BuscaDeReceitas rodar (que lançaria InvalidOperationException se
        // chegasse a rodar sem nenhuma tentativa bem-sucedida).
        await acao.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BuscarMelhorEncaixe_GiroLivrePermitePecaGirada_AindaAssimEncaixaTudo()
    {
        // Peça retangular alta e estreita, giro livre — só cabe deitada num tecido estreito.
        var pecas = new[] { Retangulo("A", 5, 40, giro: TipoDeGiro.Livre) };

        var resultado = await _servico.BuscarMelhorEncaixeAsync(pecas, ConfigRapida(larguraTecido: 45));

        resultado.ItensNaoEncaixados.Should().BeEmpty();
    }

    /// <summary>Duas peças 10×10 se sobrepõem se seus retângulos AABB se cruzam — suficiente pra pieces retangulares (todas as usadas em <see cref="Retangulo"/>).</summary>
    private static bool SeSobrepoem(ItemDeResultado a, ItemDeResultado b) =>
        a.X < b.X + b.LarguraCm && b.X < a.X + a.LarguraCm &&
        a.Y < b.Y + b.AlturaCm && b.Y < a.Y + a.AlturaCm;

    [Fact]
    public async Task BuscarMelhorEncaixe_ModoAutomatico_ReceitaNfpParticipaDaDisputaSemLancar()
    {
        // Base pass (§11.7) roda toda receita candidata pelo menos 1x, incluindo "nfp/solta/area/encosta"
        // (agora despachada, ver GeradorDeReceitas) — se o despacho tivesse algum bug de wiring
        // (contorno errado, índice trocado etc.), isso lançaria durante a própria busca e o teste
        // falharia antes mesmo de chegar nas asserções abaixo.
        var pecas = new[] { Retangulo("A", 20, 10, quantidade: 3), Retangulo("B", 15, 15, quantidade: 2) };

        await _servico.BuscarMelhorEncaixeAsync(pecas, ConfigRapida());

        var memoria = await _memoria.ConsultarMemoriaAsync(AssinaturaDoTrabalho(pecas, 150));
        memoria.Receitas.Should().ContainKey("nfp/solta/area/encosta");
        memoria.Receitas["nfp/solta/area/encosta"].Usos.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BuscarMelhorEncaixe_QualquerReceitaVencedora_ResultadoFinalNuncaTemPecasSobrepostas()
    {
        // Não força NFP a vencer (a disputa decide sozinha) — mas se ela vencer, a "regra de
        // segurança" (§7 do guia de melhorias) tem que ter passado: nenhuma sobreposição real
        // no resultado final, seja qual for o motor escolhido.
        var pecas = new[] { Retangulo("A", 12, 8, quantidade: 4), Retangulo("B", 9, 9, quantidade: 3) };
        var config = new ConfiguracaoDeEncaixe(30, 0.3, 1, TempoMaximoMs: 2_000, MsSemGanhoParaParedeMs: 500);

        var resultado = await _servico.BuscarMelhorEncaixeAsync(pecas, config);

        resultado.ItensNaoEncaixados.Should().BeEmpty();
        for (var i = 0; i < resultado.Posicoes.Count; i++)
            for (var j = i + 1; j < resultado.Posicoes.Count; j++)
                SeSobrepoem(resultado.Posicoes[i], resultado.Posicoes[j]).Should().BeFalse(
                    $"{resultado.Posicoes[i].PecaId} e {resultado.Posicoes[j].PecaId} não podem se sobrepor (receita vencedora: {resultado.ReceitaVencedora})");
    }
}
