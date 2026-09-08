namespace OptimizePro.Core.Encaixe.Busca;

public enum ModoDeMelhoria { Explorar, Refinar }

/// <summary>
/// <see cref="PiorUnidadeItens"/> (02/09/2026, porte do "reparo guiado" do projeto de
/// referência) — os índices, na <c>ordem</c> desta tentativa, da unidade que sobrou mais
/// buraco morto acima dela; <c>null</c> quando o motor não tem essa noção (retângulo/NFP) ou
/// não assentou nenhuma unidade.
/// </summary>
public sealed record ResultadoDeTentativa(double ConsumoCm, int ItensNaoEncaixados, IReadOnlyList<int>? PiorUnidadeItens = null);

/// <summary>
/// Uma linha do placar ao final da busca (§12.1) — só receitas com pelo menos 1 tentativa. Vira
/// exemplo de treino da rede das receitas e/ou registro de uso na memória por assinatura.
/// <see cref="MelhorConsumoCm"/> (02/09/2026, porte de melhoria do projeto de referência) é o
/// melhor consumo que ESTA receita conseguiu sozinha na busca — usado pro alvo de treino
/// contínuo (§12.1: quão perto da campeã cada receita chegou, não só "venceu/não venceu").
/// </summary>
public sealed record LinhaDoPlacar(Receita Receita, int Tentativas, int Vitorias, double? MelhorConsumoCm);

public sealed record ResultadoDaBusca(
    Receita MelhorReceita, IReadOnlyList<int> MelhorOrdem, double MelhorConsumoCm, int MelhorNaoEncaixados, int Tentativas,
    IReadOnlyList<LinhaDoPlacar> Placar);

public sealed record ParametrosDeBusca(
    long TempoMaximoMs,
    long MsSemGanhoParaParede,
    double PodaTolerancia = Poda.PodaToleranciaPadrao,
    int PodaMinimo = Poda.PodaMinimoPadrao,
    double PodaFresta = 0.15,
    double FracaoDoTempoParaPerseguir = 0.60);

/// <summary>
/// Porte de <c>buscarMelhorEncaixe</c> (§11.7) — o "cérebro" que decide qual receita testar
/// a cada tentativa. Desacoplado dos tipos concretos de item de cada motor (que variam:
/// <see cref="ItemEncaixe"/> para contorno/faixas, <see cref="ItemParaCaixa"/> para
/// retângulo) via os delegates <paramref name="obterOrdemBase"/>/<paramref name="executar"/>
/// — quem chama sabe mapear índice→item e rodar o motor certo por <see cref="MotorDeEncaixe"/>.
/// </summary>
/// <remarks>
/// "Perseguir recorde" (§11.7) depende da memória de aprendizado (§11.12, ainda não
/// portada) para dar um <c>alvoConsumoCm</c> — aqui é um parâmetro opcional: quando null,
/// "perseguindo" nunca ativa, igual ao comportamento sem memória disponível.
/// </remarks>
public static class BuscaDeReceitas
{
    /// <summary>Chance de usar "reparo guiado" em vez de "leve" ao refinar (§11.7, mesmo valor do <c>REPARO_CHANCE</c> do projeto de referência — medido A/B contra dados reais: consistente, ~0,1-0,3% melhor).</summary>
    private const double ReparoGuiadoChance = 0.3;

    public static ResultadoDaBusca BuscarMelhorEncaixe(
        IReadOnlyList<Receita> receitas,
        int quantidadeDeItens,
        Func<CriterioDeOrdem, IReadOnlyList<int>> obterOrdemBase,
        Func<Receita, IReadOnlyList<int>, CancellationToken, ResultadoDeTentativa> executar,
        ParametrosDeBusca parametros,
        IRelogioDeBusca relogio,
        Random aleatorio,
        double? alvoConsumoCm = null,
        CancellationToken cancelamento = default,
        int? tetoDeTentativasParaTeste = null,
        Func<Receita, double>? pesoDaReceita = null)
    {
        if (receitas.Count == 0)
            throw new ArgumentException("É preciso pelo menos uma receita.", nameof(receitas));

        var placares = receitas.ToDictionary(r => r, r => new PlacarDeReceita(r));

        PlacarDeReceita? melhorGlobal = null;
        IReadOnlyList<int>? melhorOrdemGlobal = null;
        IReadOnlyList<int>? melhorPiorUnidadeGlobal = null;
        var totalDeTentativas = 0;
        long ultimoGanhoMs = 0;
        var modo = ModoDeMelhoria.Explorar;

        void RodarTentativa(Receita receita, IReadOnlyList<int> ordem)
        {
            if (cancelamento.IsCancellationRequested)
                return;

            var resultado = executar(receita, ordem, cancelamento);
            totalDeTentativas++;

            // Captura o melhor global ANTES de registrar (bug real achado em produção,
            // 02/09/2026 — usuário notou "melhor até agora" no progresso mostrando um valor
            // que nunca aparecia no resultado final): quando a MESMA receita que já é a melhor
            // global melhora AINDA MAIS (acha uma ordem melhor pra si mesma), `placar` abaixo é
            // o MESMO OBJETO que `melhorGlobal` já aponta — `placar.Registrar(...)` já mutava
            // `melhorGlobal.MelhorConsumoCm` PARA O NOVO VALOR antes da comparação seguinte
            // rodar, virando uma auto-comparação (`novoValor < novoValor` = sempre falso). O
            // "if" abaixo nunca disparava nesse caso, então `melhorOrdemGlobal` ficava PRESO
            // numa ordem antiga — enquanto `melhorGlobal.MelhorConsumoCm` (lido no fim, direto
            // do placar) já refletia o valor novo. Resultado: o número mostrado bate com uma
            // tentativa de verdade, mas a ordem reconstruída no final é de OUTRA tentativa
            // (pior) da mesma receita — reconstruir com essa ordem "errada" nunca reproduzia o
            // consumo relatado. Medido num lote real: divergência de ~1 a ~3cm em praticamente
            // toda busca — não era raro, era sistemático sempre que a receita vencedora se
            // auto-superava (comum no modo "Refinar"). Corrigido comparando contra o valor
            // capturado ANTES do Registrar mutar o placar.
            (int NaoEncaixados, double ConsumoCm)? melhorAntesDoRegistro =
                melhorGlobal is null ? null : (melhorGlobal.MelhorNaoEncaixados, melhorGlobal.MelhorConsumoCm!.Value);

            var placar = placares[receita];
            placar.Registrar(resultado.ConsumoCm, resultado.ItensNaoEncaixados, ordem, resultado.PiorUnidadeItens);

            // Menos peça de fora sempre vence (§11.7, porte de melhoria do projeto de
            // referência — bug real de lá que o nosso port também tinha: comparar só por
            // ConsumoCm deixa uma tentativa incompleta parecer "melhor" só por gastar menos
            // tecido, quando na verdade sobrou peça sem encaixar). Ver PlacarDeReceita.EhMelhor.
            if (melhorAntesDoRegistro is null ||
                PlacarDeReceita.EhMelhor(resultado.ItensNaoEncaixados, resultado.ConsumoCm, melhorAntesDoRegistro.Value.NaoEncaixados, melhorAntesDoRegistro.Value.ConsumoCm))
            {
                melhorGlobal = placar;
                melhorOrdemGlobal = ordem;
                melhorPiorUnidadeGlobal = resultado.PiorUnidadeItens;
                ultimoGanhoMs = relogio.TempoDecorridoMs;
                placar.RegistrarVitoriaGlobal();
            }
        }

        // 1) PASSADA BASE — cada receita 1x, sem embaralhar, da mais promissora pra menos
        // segundo a memória (§11.12/§12.1 — maior peso primeiro; sem função de peso, ordem
        // de entrada é preservada por ser um OrderBy estável com peso 0 constante).
        var peso = pesoDaReceita ?? (_ => 0.0);
        foreach (var receita in receitas.OrderByDescending(peso))
            RodarTentativa(receita, obterOrdemBase(receita.Ordem));

        // 2) MELHORIA
        while (!cancelamento.IsCancellationRequested &&
               relogio.TempoDecorridoMs < parametros.TempoMaximoMs &&
               (tetoDeTentativasParaTeste is null || totalDeTentativas < tetoDeTentativasParaTeste))
        {
            var fracaoDoTempo = parametros.TempoMaximoMs > 0 ? (double)relogio.TempoDecorridoMs / parametros.TempoMaximoMs : 1.0;
            var perseguindo = alvoConsumoCm is not null
                && (melhorGlobal is null || melhorGlobal.MelhorConsumoCm > alvoConsumoCm)
                && fracaoDoTempo < parametros.FracaoDoTempoParaPerseguir;

            var semGanhoHaMs = relogio.TempoDecorridoMs - ultimoGanhoMs;
            var bateuNaParede = !perseguindo && semGanhoHaMs >= parametros.MsSemGanhoParaParede;

            if (bateuNaParede)
            {
                modo = modo == ModoDeMelhoria.Explorar ? ModoDeMelhoria.Refinar : ModoDeMelhoria.Explorar;
                ultimoGanhoMs = relogio.TempoDecorridoMs; // evita reentrar na parede a cada iteração seguinte
            }

            var podaAtiva = !bateuNaParede;

            var naRoda = Poda.ReceitasNaRoda(
                [.. placares.Values], melhorGlobal?.MelhorConsumoCm, podaAtiva,
                parametros.PodaTolerancia, parametros.PodaMinimo);

            var ignoraPoda = aleatorio.NextDouble() < parametros.PodaFresta;
            var pool = ignoraPoda ? placares.Values.ToList() : naRoda;

            var escolhido = pool[aleatorio.Next(pool.Count)];

            // "Refinar" usa Leve (troca aleatória em qualquer posição) — testamos
            // Embaralhamento.ReconstruirRabo ("ruin and recreate" focado no trecho final da
            // ordem, §9.3 item 3) como substituto e NÃO mediu ganho real contra dados reais
            // (empatado com Leve dentro da variação normal entre rodadas — ver §11.7 na
            // especificação). Mantido em Leve; ReconstruirRabo continua no Core, testado, pronto
            // se algum dia surgir evidência real de que ajuda.
            //
            // "Reparo guiado" (02/09/2026, porte do projeto de referência) entra ANTES de Leve,
            // com uma chance fixa, só quando a melhor tentativa até agora tem uma "pior unidade"
            // conhecida (motor de contorno; retângulo/NFP nunca preenchem isso) — mexe só nela
            // em vez de sacudir a ordem inteira às cegas.
            IReadOnlyList<int> ordemParaTentativa;
            if (modo == ModoDeMelhoria.Refinar && melhorOrdemGlobal is not null)
            {
                ordemParaTentativa = melhorPiorUnidadeGlobal is { Count: > 0 } && aleatorio.NextDouble() < ReparoGuiadoChance
                    ? Embaralhamento.RepararPior(melhorOrdemGlobal, melhorPiorUnidadeGlobal, aleatorio)
                    : Embaralhamento.Leve(melhorOrdemGlobal, aleatorio);
            }
            else
            {
                ordemParaTentativa = Embaralhamento.Forte(quantidadeDeItens, aleatorio);
            }

            RodarTentativa(escolhido.Receita, ordemParaTentativa);
        }

        if (melhorGlobal is null)
        {
            throw new InvalidOperationException(
                "Nenhuma tentativa rodou — o CancellationToken já estava cancelado antes de começar, " +
                "ou TempoMaximoMs é zero.");
        }

        var placarFinal = placares.Values
            .Where(p => p.Tentativas > 0)
            .Select(p => new LinhaDoPlacar(p.Receita, p.Tentativas, p.Vitorias, p.MelhorConsumoCm))
            .ToList();

        return new ResultadoDaBusca(melhorGlobal.Receita, melhorOrdemGlobal!, melhorGlobal.MelhorConsumoCm!.Value, melhorGlobal.MelhorNaoEncaixados, totalDeTentativas, placarFinal);
    }
}
