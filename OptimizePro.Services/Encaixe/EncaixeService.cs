using System.Diagnostics;
using OptimizePro.Core;
using OptimizePro.Core.Encaixe;
using OptimizePro.Core.Encaixe.Busca;
using OptimizePro.Core.Encaixe.Nfp;

namespace OptimizePro.Services.Encaixe;

/// <summary>Um item já rasterizado, pronto pro motor de contorno — máscaras cacheadas por rotação permitida (§11.10: "uma vez por busca").</summary>
internal sealed record ItemContorno(ItemEncaixe Item, IReadOnlyList<int> RotacoesPermitidas, double AreaRealCm2);

/// <summary>O mesmo item, na forma que o motor de retângulo consome (só a caixa delimitadora importa).</summary>
internal sealed record ItemCaixa(ItemParaCaixa Item, bool PermiteDeitar);

/// <summary>
/// <see cref="PiorUnidadeItens"/> (02/09/2026, porte do "reparo guiado" do projeto de
/// referência, §11.7) — os índices (em <c>ordem</c>) da unidade que sobrou mais buraco morto
/// acima dela nesta tentativa do motor de CONTORNO; <c>null</c> pros outros motores (retângulo/
/// NFP não têm esse conceito) ou quando nenhuma unidade foi assentada.
/// </summary>
internal readonly record struct ResultadoMotor(
    double ConsumoCm, int NaoEncaixados, IReadOnlyList<ItemDeResultado> Posicoes, IReadOnlyList<string> IdsNaoEncaixados, double AreaRealCm2,
    IReadOnlyList<int>? PiorUnidadeItens = null);

public sealed class EncaixeService(IEncaixeMemoriaService memoria) : IEncaixeService
{
    /// <summary>Tolerância (cm) de resimplificação específica pro NFP — ver nota na montagem de <c>itensNfp</c> abaixo, em <see cref="BuscarMelhorEncaixeAsync"/>.</summary>
    private const double ToleranciaDeSimplificacaoParaNfpCm = 1.5;

    public async Task<ResultadoDeEncaixe> BuscarMelhorEncaixeAsync(
        IReadOnlyList<PecaParaEncaixar> pecas,
        ConfiguracaoDeEncaixe config,
        IProgress<AndamentoDoEncaixe>? progresso = null,
        CancellationToken cancelamento = default)
    {
        if (pecas.Count == 0)
            throw new ArgumentException("Nenhuma peça informada.", nameof(pecas));

        // 1) Peças expandem em itens — 1 cópia cada (§11.1).
        var itensExpandidos = pecas
            .SelectMany(p => Enumerable.Range(0, p.Quantidade).Select(k => (Peca: p, ItemId: $"{p.Id}#{k}")))
            .ToList();

        if (itensExpandidos.Count == 0)
            throw new ArgumentException("Nenhum item pra encaixar (quantidade zero em todas as peças).", nameof(pecas));

        var quantidadeDeItens = itensExpandidos.Count;

        // 2) Grade (§11.2) — passo de célula e raio de folga a partir do espaço pedido.
        var grade = Grade.Calcular(config.LarguraTecidoCm, config.EspacoCm);

        // Épsilon antes do Ceiling (§9.3, "verifique o cálculo de espaçamento"): quando a
        // largura do tecido divide exatamente pelo passo (ex.: 179cm/0,2cm=895), erro de ponto
        // flutuante às vezes entrega 895,0000000000001 em vez de 895,0 — sem o épsilon, o
        // Ceiling arredondava pra 896 e "colsTecido*passo" passava a reivindicar até ~1 passo
        // de largura A MAIS do que o rolo tem de verdade (medido: 137,5cm/0,15cm virava
        // 137,55cm — 0,5mm de tecido fantasma). Só protege contra esse artefato de arredondamento;
        // não muda nada quando a divisão já não é exata (aí o Ceiling real ainda é necessário).
        var colsTecido = Math.Max(1, (int)Math.Ceiling(config.LarguraTecidoCm / grade.PassoCm - 1e-6));

        // 3) Silhueta → máscaras por rotação, uma vez por item (§11.10).
        var itensContorno = new ItemContorno[quantidadeDeItens];
        var itensCaixa = new ItemCaixa[quantidadeDeItens];
        var itensNfp = new ItemParaNfp[quantidadeDeItens];
        var contornosOriginaisPorRotacaoPorId = new Dictionary<string, IReadOnlyDictionary<int, IReadOnlyList<PontoXY>>>(quantidadeDeItens);

        for (var i = 0; i < quantidadeDeItens; i++)
        {
            var (peca, itemId) = itensExpandidos[i];
            var caixaBbox = Geometria.CaixaDeContorno(peca.Contorno);
            var areaReal = Math.Abs(Geometria.AreaComSinal(peca.Contorno));
            var rotacoes = Giro.RotacoesPara(peca.Giro);

            var mascaraBase = RasterizacaoDePeca.Rasterizar(peca.Contorno, grade);
            var itemEncaixe = ItemEncaixe.DeMascaraBase(itemId, mascaraBase, rotacoes);
            itensContorno[i] = new ItemContorno(itemEncaixe, rotacoes, areaReal);

            var permiteDeitar = peca.Giro == TipoDeGiro.Livre;
            itensCaixa[i] = new ItemCaixa(new ItemParaCaixa(itemId, caixaBbox.Largura, caixaBbox.Altura, areaReal), permiteDeitar);

            // NFP (§11.5/§11.11, giro portado 02/09/2026) — gira o contorno ORIGINAL em cada
            // rotação permitida, e SÓ ENTÃO resimplifica cada variante (§7, medido): o traçado
            // de imagem (LeitorDeImagemDeEncaixe) já simplifica, só que em espaço de PIXEL antes
            // da conversão pra cm — na escala real isso deixa ~190 vértices em média por peça
            // (fotos de roupa reais), e a soma de Minkowski do NFP é ~quadrática nisso: uma
            // busca inteira estourou de 15s pra 17,7min nesse tamanho de lote. Resimplificar
            // aqui, já em cm, com tolerância maior (1,5cm — desvio de área medido < 1,1%) nunca
            // muda o veredito de segurança: a posição escolhida ainda é conferida contra o
            // contorno ORIGINAL da rotação escolhida (sem essa resimplificação) pela grade
            // discreta independente (ValidadorDeSobreposicaoNfp) antes de virar resultado — na
            // pior hipótese perde a disputa por desqualificação, nunca gera sobreposição de
            // verdade.
            var contornosOriginaisPorRotacao = new Dictionary<int, IReadOnlyList<PontoXY>>(rotacoes.Count);
            var contornosParaNfpPorRotacao = new Dictionary<int, IReadOnlyList<PontoXY>>(rotacoes.Count);
            foreach (var rot in rotacoes)
            {
                var contornoOriginalRotacionado = Geometria.Rotacionar90(peca.Contorno, rot);
                contornosOriginaisPorRotacao[rot] = contornoOriginalRotacionado;
                contornosParaNfpPorRotacao[rot] = Geometria.Simplificar(contornoOriginalRotacionado, ToleranciaDeSimplificacaoParaNfpCm);
            }

            itensNfp[i] = new ItemParaNfp(itemId, contornosParaNfpPorRotacao);
            contornosOriginaisPorRotacaoPorId[itemId] = contornosOriginaisPorRotacao;
        }

        // 3.1) Pareamento "cruzada" (§11.8) — depende só do inventário de formatos distintos e
        // da largura do tecido, nunca da ordem sorteada de uma tentativa. Computado UMA VEZ por
        // busca inteira (não por tentativa!): o custo é O(formatos²), e recalcular a cada uma
        // das centenas/milhares de tentativas de uma receita "cruzada" starva a busca do tempo
        // que sobraria pra explorar mais ordens (medido: caía de ~500 pra ~150 tentativas totais).
        var candidatosCruzados = PrecomputarCandidatosCruzados(itensContorno, colsTecido);

        // 3.2) Blocos dupla/trio (§11.8) — mesmo raciocínio da "cruzada" acima: quais formas de
        // bloco existem por peça-base (via AgrupamentoDeBlocos.FormasDoBloco, que já faz sua
        // própria busca geométrica cara em EncostoDeFormas) depende só do formato de cada peça
        // e do tamanho do bloco, nunca da ordem sorteada — computado uma vez por busca inteira.
        // Medido: sem isso, o motor de contorno fazia só ~36-72 tentativas/s (25 arquivos/57
        // peças reais) contra a ordem de milhares/s do "Retângulo" na mesma busca, porque
        // recomputava geometria de bloco pra cada peça-base distinta EM TODA TENTATIVA.
        var candidatosDeBlocoPorPeca = PrecomputarCandidatosDeBloco(itensContorno, colsTecido);

        // 4) Ordem base por critério — maior primeiro (heurística padrão de bin-packing).
        double Metrica(int indice, CriterioDeOrdem criterio)
        {
            var item = itensCaixa[indice].Item;
            return criterio switch
            {
                CriterioDeOrdem.Area => item.LarguraCm * item.AlturaCm,
                CriterioDeOrdem.Altura => item.AlturaCm,
                CriterioDeOrdem.Largura => item.LarguraCm,
                CriterioDeOrdem.Lado => Math.Max(item.LarguraCm, item.AlturaCm),
                _ => throw new ArgumentOutOfRangeException(nameof(criterio)),
            };
        }

        IReadOnlyList<int> ObterOrdemBase(CriterioDeOrdem criterio) =>
            Enumerable.Range(0, quantidadeDeItens).OrderByDescending(i => Metrica(i, criterio)).ToList();

        // 5) Dispatcher por receita — chamado a cada tentativa da busca; thread-safe (cada
        // chamada só lê os itens cacheados e aloca seu próprio "perfil"/estado local).
        var tentativasParaProgresso = 0;
        var melhorConsumoParaProgresso = double.PositiveInfinity;
        var travaProgresso = new object();
        var cronometroDeProgresso = Stopwatch.StartNew();

        ResultadoDeTentativa Executar(Receita receita, IReadOnlyList<int> ordem, CancellationToken ct)
        {
            // detalhado:false (§9.3, "reduzir alocação por tentativa") — a busca só compara
            // ConsumoCm (ver BuscaDeReceitas.RodarTentativa) e devolve ResultadoDeTentativa, que
            // nem tem campo pra posições — construir List<ItemDeResultado>/áreas reais nas
            // dezenas de milhares de tentativas perdedoras de uma busca (só a ordem VENCEDORA é
            // reconstruída de novo, em detalhe, depois — ver passo 7 abaixo) era alocação pura
            // jogada fora.
            var resultado = ExecutarReceita(receita, ordem, itensContorno, itensCaixa, itensNfp, contornosOriginaisPorRotacaoPorId, colsTecido, grade, config.MargemCm, config.LarguraTecidoCm, candidatosCruzados, candidatosDeBlocoPorPeca, detalhado: false);

            if (progresso is not null)
            {
                lock (travaProgresso)
                {
                    tentativasParaProgresso++;
                    if (resultado.ConsumoCm < melhorConsumoParaProgresso) melhorConsumoParaProgresso = resultado.ConsumoCm;
                    progresso.Report(new AndamentoDoEncaixe(tentativasParaProgresso, melhorConsumoParaProgresso, cronometroDeProgresso.ElapsedMilliseconds));
                }
            }

            return new ResultadoDeTentativa(resultado.ConsumoCm, resultado.NaoEncaixados, resultado.PiorUnidadeItens);
        }

        // 5.1) Memória de aprendizado (§11.12/§12/§12.1) — assinatura+vetor a partir das peças
        // DISTINTAS (não expandidas por quantidade, §11.12: "não por nome/quantidade"), consulta
        // as duas camadas de sempre mais a rede (se já treinada) e usa pra pesar/podar as receitas.
        var pecasParaRede = pecas.Select(p =>
        {
            var caixa = Geometria.CaixaDeContorno(p.Contorno);
            var areaReal = Math.Abs(Geometria.AreaComSinal(p.Contorno));
            var areaCaixa = caixa.Largura * caixa.Altura;
            var ocupacao = areaCaixa > 0 ? areaReal / areaCaixa : 1.0;
            return new PecaParaRede(ocupacao, caixa.Largura, caixa.Altura, p.Giro, p.Quantidade);
        }).ToList();

        var assinatura = AssinaturaDeTrabalho.Calcular(pecasParaRede, config.LarguraTecidoCm);
        var vetorTrabalho = VetorizacaoDoTrabalho.VetorDoTrabalho(pecasParaRede, config.LarguraTecidoCm);
        var memoriaDoTipo = await memoria.ConsultarMemoriaAsync(assinatura, cancelamento);

        // 6) Busca por receitas, em paralelo (§11.7/§11.9) — contorno+retângulo+NFP disputando
        // (modo automático); faixas fica pro próximo incremento (ver GeradorDeReceitas).
        var receitas = GeradorDeReceitas.GerarPadrao(config.Modo);
        var parametros = new ParametrosDeBusca(config.TempoMaximoMs, config.MsSemGanhoParaParedeMs);

        // A rede pontua mesmo antes de "madura" (só a poda depende de madureza, §12.1 passo 3);
        // sem rede treinada ainda, pontosDaRede fica vazio e o peso cai de volta pro balde exato.
        var pontosDaRede = memoriaDoTipo.Rede is not null
            ? RedeDeReceitas.PontuarReceitas(memoriaDoTipo.Rede, vetorTrabalho, receitas.Select(VocabularioDeReceita.ChaveDaReceita))
            : new Dictionary<string, double>();

        var receitasCandidatas = memoriaDoTipo.RedeMadura && pontosDaRede.Count > 0
            ? FiltrarPorRede(receitas, pontosDaRede)
            : receitas;

        double PesoDaReceita(Receita r)
        {
            var chave = VocabularioDeReceita.ChaveDaReceita(r);
            var doBalde = memoriaDoTipo.Receitas.TryGetValue(chave, out var placar) && placar.Usos > 0 ? placar.Vitorias / placar.Usos : 0.0;
            var daRede = pontosDaRede.TryGetValue(chave, out var p) ? p : 0.0;
            var historico = Math.Max(doBalde, daRede);
            return 1 + historico * 4;
        }

        var buscaParalela = await BuscaParalela.BuscarMelhorEncaixeAsync(
            receitasCandidatas, quantidadeDeItens, ObterOrdemBase, Executar, parametros,
            criarRelogio: () => new RelogioReal(),
            criarAleatorioPorFatia: _ => new Random(),
            reservarUltimaFatiaParaNfp: false,
            alvoConsumoCm: null,
            cancelamento: cancelamento,
            pesoDaReceita: PesoDaReceita);

        // 7) Reconstrói o resultado completo (posições/rotações) da receita vencedora — a
        // busca em si só precisa do consumo (ResultadoDeTentativa), não das posições.
        var melhor = buscaParalela.Melhor;
        var final = ExecutarReceita(melhor.MelhorReceita, melhor.MelhorOrdem, itensContorno, itensCaixa, itensNfp, contornosOriginaisPorRotacaoPorId, colsTecido, grade, config.MargemCm, config.LarguraTecidoCm, candidatosCruzados, candidatosDeBlocoPorPeca);

        var aproveitamento = final.ConsumoCm > 0 && config.LarguraTecidoCm > 0
            ? Math.Clamp(final.AreaRealCm2 / (config.LarguraTecidoCm * final.ConsumoCm) * 100, 0, 100)
            : 0;

        var tentativasTotais = buscaParalela.ResultadosPorFatia.Sum(r => r.Tentativas);
        var chaveVencedora = VocabularioDeReceita.ChaveDaReceita(melhor.MelhorReceita);

        // 8) Registra a busca inteira na memória (§11.12/§12.1) — nunca derruba o resultado se
        // falhar (mesma regra de "acelerador, não requisito" aplicada dentro do serviço de memória).
        try
        {
            var placar = buscaParalela.ResultadosPorFatia
                .SelectMany(r => r.Placar)
                .Select(l => new LinhaDoPlacarDto(VocabularioDeReceita.ChaveDaReceita(l.Receita), l.Tentativas, l.Vitorias, l.MelhorConsumoCm))
                .ToList();

            await memoria.RegistrarResultadoDaBuscaAsync(new ResultadoDeBuscaParaMemoria(
                assinatura, chaveVencedora, placar, vetorTrabalho,
                config.LarguraTecidoCm, quantidadeDeItens, final.ConsumoCm, aproveitamento, tentativasTotais), cancelamento);
        }
        catch
        {
            // idem — registro de memória não é requisito pro resultado do encaixe em si.
        }

        return new ResultadoDeEncaixe(final.Posicoes, final.IdsNaoEncaixados, final.ConsumoCm, aproveitamento, tentativasTotais, DescreverReceita(melhor.MelhorReceita), final.AreaRealCm2);
    }

    /// <summary>Porte de <c>filtrarPorRede</c> (§12.1) — poda receitas com pontuação abaixo do limiar, mas nunca um motor inteiro (sinal de "sem opinião", não "motor ruim").</summary>
    private static IReadOnlyList<Receita> FiltrarPorRede(IReadOnlyList<Receita> receitas, IReadOnlyDictionary<string, double> pontos)
    {
        const double limiar = 0.05; // REDE_CORTE_LIMIAR

        var resultado = new List<Receita>();
        foreach (var doMotor in receitas.GroupBy(r => r.Motor))
        {
            var passaram = doMotor.Where(r => pontos.TryGetValue(VocabularioDeReceita.ChaveDaReceita(r), out var p) && p >= limiar).ToList();
            resultado.AddRange(passaram.Count > 0 ? passaram : doMotor);
        }

        return resultado;
    }

    /// <summary>Um par de formatos que compensa juntar em bloco (§11.8, "cruzada") — computado uma vez por busca inteira, não por tentativa (ver nota em <c>PrecomputarCandidatosCruzados</c>).</summary>
    private sealed record ParCruzado(string FormatoA, string FormatoB, IReadOnlyList<Forma> Formas);

    private static ResultadoMotor ExecutarReceita(
        Receita receita, IReadOnlyList<int> ordem,
        IReadOnlyList<ItemContorno> itensContorno, IReadOnlyList<ItemCaixa> itensCaixa, IReadOnlyList<ItemParaNfp> itensNfp,
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, IReadOnlyList<PontoXY>>> contornosOriginaisPorRotacaoPorId,
        int colsTecido, Grade grade, double margemCm, double larguraTecidoCm,
        IReadOnlyList<ParCruzado> candidatosCruzados,
        IReadOnlyDictionary<(string PecaBase, int Tamanho), IReadOnlyList<Forma>> candidatosDeBlocoPorPeca,
        bool detalhado = true) =>
        receita.Motor switch
        {
            MotorDeEncaixe.Contorno => ExecutarContorno(receita, ordem, itensContorno, colsTecido, grade.PassoCm, margemCm, candidatosCruzados, candidatosDeBlocoPorPeca, detalhado),
            MotorDeEncaixe.Retangulo => ExecutarRetangulo(receita, ordem, itensCaixa, larguraTecidoCm, margemCm),
            MotorDeEncaixe.Nfp => ExecutarNfp(ordem, itensNfp, contornosOriginaisPorRotacaoPorId, larguraTecidoCm, margemCm, grade),
            MotorDeEncaixe.Vaos => ExecutarVaos(ordem, itensContorno, colsTecido, grade.PassoCm, margemCm, detalhado),
            _ => throw new NotSupportedException($"Motor {receita.Motor} ainda não despachado (faixas ficam pro próximo incremento)."),
        };

    /// <summary>
    /// Porte da metade "não depende de tentativa" de <c>montarUnidades</c> pra blocos dupla/trio
    /// (§11.8) — igual a <see cref="PrecomputarCandidatosCruzados"/>: quais formas de bloco
    /// existem por peça-base só depende do FORMATO da peça e do tamanho do bloco, nunca da
    /// ordem sorteada. <see cref="AgrupamentoDeBlocos.FormasDoBloco"/> por baixo dos panos
    /// aciona <c>EncostoDeFormas.EncostarNaForma</c> (busca geométrica O(largura²) própria) —
    /// recomputar isso por peça-base EM TODA TENTATIVA (como fazia antes) era o maior custo
    /// escondido do motor de contorno: medido em dados reais (25 arquivos/57 peças), o motor
    /// fazia só ~36-72 tentativas/s contra a ordem de milhares/s do motor de retângulo na
    /// MESMA busca, fazendo "Retângulo" vencer por vantagem de tentativas, não por ser
    /// genuinamente melhor pra essas peças.
    /// </summary>
    private static IReadOnlyDictionary<(string PecaBase, int Tamanho), IReadOnlyList<Forma>> PrecomputarCandidatosDeBloco(
        IReadOnlyList<ItemContorno> itens, int colsTecido)
    {
        var representantePorFormato = new Dictionary<string, ItemContorno>();
        foreach (var item in itens)
            representantePorFormato.TryAdd(item.Item.Id.Split('#')[0], item);

        var resultado = new Dictionary<(string, int), IReadOnlyList<Forma>>();
        foreach (var (pecaBase, item) in representantePorFormato)
        {
            // Só dupla(2)/trio(3) — quarteto(4) fora da lista padrão de agrupamentos (§11.8:
            // "o próprio original mediu e não compensou o custo de mais uma receita").
            foreach (var tamanho in (int[])[2, 3])
            {
                IReadOnlyList<Forma> candidatos = !item.RotacoesPermitidas.Contains(180) || !item.Item.MascarasPorRotacao.TryGetValue(0, out var mascaraBase)
                    ? []
                    : [.. AgrupamentoDeBlocos.FormasDoBloco(mascaraBase, tamanho).Where(f => f.Colunas <= colsTecido)];
                resultado[(pecaBase, tamanho)] = candidatos;
            }
        }

        return resultado;
    }

    /// <summary>
    /// Porte de <c>montarUnidades</c>+laço externo de <c>encaixe-motor.js</c> (§11.3/§11.8).
    /// Pra "solta" cada peça é sua própria unidade (1 item); pra "dupla"/"trio" as cópias da
    /// MESMA peça (por id-base, sem o sufixo "#k") viram um bloco só — a cópia normal mais a
    /// invertida 180° que fecha o vão da outra (pescoço, cava). Peça que não aceita giro 180°
    /// nunca forma bloco, e sobra que não completa um bloco entra solta — igual ao original.
    /// </summary>
    private static ResultadoMotor ExecutarContorno(
        Receita receita, IReadOnlyList<int> ordem, IReadOnlyList<ItemContorno> itens, int colsTecido, double passoCm, double margemCm,
        IReadOnlyList<ParCruzado> candidatosCruzados,
        IReadOnlyDictionary<(string PecaBase, int Tamanho), IReadOnlyList<Forma>> candidatosDeBlocoPorPeca,
        bool detalhado = true)
    {
        var tamanhoDoBloco = receita.Agrupamento switch
        {
            AgrupamentoDeEncaixe.Dupla => 2,
            AgrupamentoDeEncaixe.Trio => 3,
            AgrupamentoDeEncaixe.Quarteto => 4,
            _ => 1,
        };

        var heuristica = receita.HeuristicaContorno!.Value;

        // Pool de array (§9.3, "reduzir alocação por tentativa") — este método roda dezenas de
        // milhares de vezes numa busca (uma por tentativa, em cada fatia paralela); "perfil"
        // (na casa das centenas/milhares de colunas) sendo `new int[colsTecido]` toda vez virava
        // GC pressure real. Array alugado pode vir MAIOR que colsTecido (comportamento normal do
        // pool) — sem problema, nenhum índice usado passa de colsTecido-1 (ver
        // EncaixadorPorContorno.MelhorPosicaoDaUnidade, que já limita xMax por colsTecido).
        var perfil = System.Buffers.ArrayPool<int>.Shared.Rent(colsTecido);
        try
        {
            Array.Clear(perfil, 0, colsTecido);
            return ExecutarContornoComPerfil(receita, ordem, itens, colsTecido, passoCm, margemCm, candidatosCruzados, candidatosDeBlocoPorPeca, tamanhoDoBloco, heuristica, perfil, detalhado);
        }
        finally
        {
            System.Buffers.ArrayPool<int>.Shared.Return(perfil);
        }
    }

    private static ResultadoMotor ExecutarContornoComPerfil(
        Receita receita, IReadOnlyList<int> ordem, IReadOnlyList<ItemContorno> itens, int colsTecido, double passoCm, double margemCm,
        IReadOnlyList<ParCruzado> candidatosCruzados,
        IReadOnlyDictionary<(string PecaBase, int Tamanho), IReadOnlyList<Forma>> candidatosDeBlocoPorPeca,
        int tamanhoDoBloco, HeuristicaDeContorno heuristica, int[] perfil, bool detalhado)
    {
        // Modo "leve" (detalhado:false, §9.3): a busca só compara ConsumoCm — pula a alocação de
        // ItemDeResultado/List<string> que ninguém ia ler mesmo (ver comentário em `Executar`,
        // no chamador). Só a reconstrução final (detalhado:true) monta essas listas de verdade.
        List<ItemDeResultado>? posicoes = detalhado ? [] : null;
        List<string>? idsNaoEncaixados = detalhado ? [] : null;
        var naoEncaixadosCount = 0;
        var areaReal = 0.0;
        var fundoMaximo = 0;

        // "Reparo guiado" (02/09/2026, §11.7): rastreia qual unidade assentada sobrou mais
        // buraco morto (o "vazio" que o motor já calcula pra toda posição, mesmo quando a
        // heurística vencedora é "fundo" — ver PosicaoEncontrada.P1/P2) — quase de graça, já
        // que o valor já está calculado; só guarda o pior. Fica disponível pro modo "Refinar"
        // tentar consertar ESSA unidade específica em vez de embaralhar tudo às cegas.
        IReadOnlyList<int>? piorUnidadeItens = null;
        var piorVazio = long.MinValue;

        IReadOnlyList<Forma> CandidatosDaUnidade(IReadOnlyList<int> unidade)
        {
            if (unidade.Count == 1)
            {
                var item = itens[unidade[0]];
                var lista = new List<Forma>(item.RotacoesPermitidas.Count);
                foreach (var rot in item.RotacoesPermitidas)
                {
                    if (!item.Item.MascarasPorRotacao.TryGetValue(rot, out var mascara)) continue;
                    var forma = Forma.DeMascaraUnica(mascara, rot);
                    if (forma.Colunas <= colsTecido) lista.Add(forma);
                }
                return lista;
            }

            var primeiro = itens[unidade[0]];
            var pecaBase = primeiro.Item.Id.Split('#')[0];

            return candidatosDeBlocoPorPeca.TryGetValue((pecaBase, unidade.Count), out var candidatos) ? candidatos : [];
        }

        (Forma Forma, PosicaoEncontrada Posicao)? MelhorEntreCandidatos(IReadOnlyList<Forma> candidatos)
        {
            (Forma Forma, PosicaoEncontrada Posicao)? melhor = null;
            foreach (var forma in candidatos)
            {
                // V2 experimental (§9.3) — pra reverter, troca de volta pra MelhorPosicaoDaUnidade
                // (V1) nesta única linha; a V2 em si fica intocada em EncaixadorPorContorno.cs,
                // sem precisar desfazer nada além disso.
                var pos = EncaixadorPorContorno.MelhorPosicaoDaUnidadeV2(perfil, colsTecido, forma, heuristica);
                if (pos is not { } p) continue;
                if (melhor is null || p.P1 < melhor.Value.Posicao.P1 || (p.P1 == melhor.Value.Posicao.P1 && p.P2 < melhor.Value.Posicao.P2))
                    melhor = (forma, p);
            }
            return melhor;
        }

        void Assentar((Forma Forma, PosicaoEncontrada Posicao)? escolha, IReadOnlyList<int> unidade)
        {
            if (escolha is not { } e)
            {
                naoEncaixadosCount += unidade.Count;
                if (idsNaoEncaixados is not null)
                    foreach (var indice in unidade) idsNaoEncaixados.Add(itens[indice].Item.Id);
                return;
            }

            var (forma, pos) = e;
            for (var c = 0; c < forma.Colunas; c++)
            {
                if (forma.Topo[c] < 0) continue;
                perfil[pos.X + c] = pos.Y + forma.Base[c] + 1;
            }

            var fundoDaForma = pos.Y + forma.MaxBase + 1;
            if (fundoDaForma > fundoMaximo) fundoMaximo = fundoDaForma;

            // "vazio" está em P1 (heurística Vazio) ou P2 (heurística Fundo) — Contato não tem
            // essa noção (P1/P2 são outra coisa lá), então essa unidade nunca vira "pior" pra ele.
            var vazioDestaUnidade = heuristica switch
            {
                HeuristicaDeContorno.Vazio => pos.P1,
                HeuristicaDeContorno.Fundo => pos.P2,
                _ => long.MinValue,
            };
            if (vazioDestaUnidade > piorVazio)
            {
                piorVazio = vazioDestaUnidade;
                piorUnidadeItens = unidade;
            }

            if (posicoes is null) return; // modo leve: só o relevo/fundoMaximo importa pro ConsumoCm.

            for (var k = 0; k < forma.Partes.Count; k++)
            {
                var parte = forma.Partes[k];
                var item = itens[unidade[k]];
                var alturaParte = Forma.DeMascaraUnica(parte.Mascara).MaxBase + 1;

                areaReal += item.AreaRealCm2;
                posicoes.Add(new ItemDeResultado(
                    item.Item.Id,
                    (pos.X + parte.DeslocamentoColuna) * passoCm,
                    (pos.Y + parte.DeslocamentoLinha) * passoCm,
                    parte.Mascara.Colunas * passoCm,
                    alturaParte * passoCm,
                    parte.RotacaoGraus));
            }
        }

        var unidades = receita.Agrupamento == AgrupamentoDeEncaixe.Cruzada
            ? MontarUnidadesCruzadas(ordem, itens, candidatosCruzados)
            : [.. MontarUnidades(ordem, itens, tamanhoDoBloco).Select(lista => new UnidadeContorno(lista, null))];

        foreach (var unidade in unidades)
        {
            var candidatos = unidade.CandidatosPreComputados ?? CandidatosDaUnidade(unidade.Itens);
            var escolha = MelhorEntreCandidatos(candidatos);

            // Bloco que não coube/não compensou (§11.8: "só vale se render menos que as peças
            // separadas") — cada cópia sai solta, nunca pior que a receita "solta" ao lado dela.
            if (escolha is null && unidade.Itens.Count > 1)
            {
                foreach (var indice in unidade.Itens)
                    Assentar(MelhorEntreCandidatos(CandidatosDaUnidade([indice])), [indice]);
                continue;
            }

            Assentar(escolha, unidade.Itens);
        }

        var consumo = fundoMaximo * passoCm + margemCm * 2;
        return new ResultadoMotor(consumo, naoEncaixadosCount, posicoes ?? [], idsNaoEncaixados ?? [], areaReal, piorUnidadeItens);
    }

    /// <summary>
    /// Porte de <c>encaixarPorVaos</c> (§21.3 da spec) — mesma ideia de laço do contorno, só
    /// que sobre <see cref="TecidoPorVaos"/> (intervalos por coluna) em vez de um relevo só,
    /// via <see cref="EncaixadorPorVaos"/>. Escopo desta primeira versão: só peça avulsa —
    /// sem a máquina de blocos/cruzada que <see cref="ExecutarContorno"/> tem (ver o
    /// comentário de escopo em <c>EncaixadorPorVaos.cs</c>).
    /// </summary>
    private static ResultadoMotor ExecutarVaos(
        IReadOnlyList<int> ordem, IReadOnlyList<ItemContorno> itens, int colsTecido, double passoCm, double margemCm, bool detalhado)
    {
        var tecido = new TecidoPorVaos(colsTecido);

        List<ItemDeResultado>? posicoes = detalhado ? [] : null;
        List<string>? idsNaoEncaixados = detalhado ? [] : null;
        var naoEncaixadosCount = 0;
        var areaReal = 0.0;
        var fundoMaximo = 0;
        IReadOnlyList<int>? piorUnidadeItens = null;
        var piorVazio = long.MinValue;

        foreach (var indice in ordem)
        {
            var item = itens[indice];

            (Forma Forma, PosicaoEncontrada Posicao)? melhor = null;
            foreach (var rot in item.RotacoesPermitidas)
            {
                if (!item.Item.MascarasPorRotacao.TryGetValue(rot, out var mascara)) continue;
                var forma = Forma.DeMascaraUnica(mascara, rot);
                if (forma.Colunas > colsTecido) continue;

                var pos = EncaixadorPorVaos.MelhorVaga(tecido, forma);
                if (pos is not { } p) continue;
                if (melhor is null || p.P1 < melhor.Value.Posicao.P1)
                    melhor = (forma, p);
            }

            if (melhor is not { } escolha)
            {
                naoEncaixadosCount++;
                idsNaoEncaixados?.Add(item.Item.Id);
                continue;
            }

            var (forma2, pos2) = escolha;
            EncaixadorPorVaos.Ocupar(tecido, forma2, pos2.X, pos2.Y, indice);

            var fundoDaForma = pos2.Y + forma2.MaxBase + 1;
            if (fundoDaForma > fundoMaximo) fundoMaximo = fundoDaForma;

            // "Vazio" pro reparo guiado, mesma fórmula do contorno (ver Assentar em
            // ExecutarContornoComPerfil) — o motor de vãos não escolhe por vazio, mas o valor
            // continua servindo pra achar a unidade que sobrou com mais buraco morto acima dela.
            var vazio = pos2.Y * (long)forma2.NumeroDeColunasValidas + forma2.SomaTopo;
            if (vazio > piorVazio)
            {
                piorVazio = vazio;
                piorUnidadeItens = [indice];
            }

            if (posicoes is null) continue;

            areaReal += item.AreaRealCm2;
            posicoes.Add(new ItemDeResultado(
                item.Item.Id,
                pos2.X * passoCm,
                pos2.Y * passoCm,
                forma2.Colunas * passoCm,
                (forma2.MaxBase + 1) * passoCm,
                forma2.Partes[0].RotacaoGraus));
        }

        var consumo = fundoMaximo * passoCm + margemCm * 2;
        return new ResultadoMotor(consumo, naoEncaixadosCount, posicoes ?? [], idsNaoEncaixados ?? [], areaReal, piorUnidadeItens);
    }

    /// <summary>Uma unidade de placement do contorno — 1+ peças assentadas de uma vez. "Cruzada" já chega com as formas candidatas prontas (não dá pra recomputar por peça-base, já que junta formatos diferentes); dupla/trio/solta computam sob demanda via <c>CandidatosDaUnidade</c>.</summary>
    private sealed record UnidadeContorno(IReadOnlyList<int> Itens, IReadOnlyList<Forma>? CandidatosPreComputados);

    /// <summary>Porte de <c>montarUnidades</c> (§11.8) — agrupa cópias da mesma peça-base em blocos de <paramref name="tamanhoDoBloco"/>, na ordem em que aparecem em <paramref name="ordem"/>; sobra e peça sem giro 180° saem soltas.</summary>
    private static List<List<int>> MontarUnidades(IReadOnlyList<int> ordem, IReadOnlyList<ItemContorno> itens, int tamanhoDoBloco)
    {
        if (tamanhoDoBloco <= 1)
            return [.. ordem.Select(i => new List<int> { i })];

        var unidades = new List<List<int>>();
        var pendentesPorPeca = new Dictionary<string, List<int>>();

        foreach (var indice in ordem)
        {
            var item = itens[indice];
            if (!item.RotacoesPermitidas.Contains(180))
            {
                unidades.Add([indice]);
                continue;
            }

            var pecaBase = item.Item.Id.Split('#')[0];
            if (!pendentesPorPeca.TryGetValue(pecaBase, out var lista))
            {
                lista = [];
                pendentesPorPeca[pecaBase] = lista;
            }
            lista.Add(indice);

            if (lista.Count == tamanhoDoBloco)
            {
                unidades.Add([.. lista]);
                lista.Clear();
            }
        }

        foreach (var lista in pendentesPorPeca.Values)
            foreach (var indice in lista)
                unidades.Add([indice]);

        return unidades;
    }

    /// <summary>Nenhum ganho de conferir todo par de formatos acima disto — um trabalho tão variado já se vira sozinho com dupla/trio (§11.8, mesmo limite do original).</summary>
    private const int CruzadaMaximoDeFormatos = 60;

    /// <summary>
    /// Porte de metade de <c>montarUnidadesCruzadas</c> (§11.8, "cruzada") — a metade que NÃO
    /// depende de tentativa nenhuma: mede quanto cada PAR de formatos DISTINTOS economiza
    /// (<see cref="AgrupamentoCruzado"/>) e ordena do que mais economiza pro que menos. Chamado
    /// UMA VEZ por busca inteira (não por tentativa) — o custo é O(formatos²), e como o
    /// pareamento não depende da ordem sorteada (é sempre o mesmo cálculo guloso do original),
    /// recalcular a cada tentativa só rouba tempo de busca sem mudar o resultado.
    /// </summary>
    private static IReadOnlyList<ParCruzado> PrecomputarCandidatosCruzados(IReadOnlyList<ItemContorno> itens, int colsTecido)
    {
        var representantePorFormato = new Dictionary<string, ItemContorno>();
        foreach (var item in itens)
            representantePorFormato.TryAdd(item.Item.Id.Split('#')[0], item);

        var formatos = representantePorFormato.Keys.ToList();
        if (formatos.Count < 2 || formatos.Count > CruzadaMaximoDeFormatos)
            return [];

        var candidatos = new List<(ParCruzado Par, double Economia)>();
        for (var i = 0; i < formatos.Count; i++)
        {
            var itemA = representantePorFormato[formatos[i]];
            for (var j = i + 1; j < formatos.Count; j++)
            {
                var itemB = representantePorFormato[formatos[j]];
                var arranjo = AgrupamentoCruzado.Tentar(itemA.Item.MascarasPorRotacao, itemB.Item.MascarasPorRotacao);
                if (arranjo is null) continue;

                var formasQueCabem = arranjo.Formas.Where(f => f.Colunas <= colsTecido).ToList();
                if (formasQueCabem.Count == 0) continue;

                candidatos.Add((new ParCruzado(formatos[i], formatos[j], formasQueCabem), arranjo.Economia));
            }
        }

        return [.. candidatos.OrderByDescending(c => c.Economia).Select(c => c.Par)];
    }

    /// <summary>
    /// Porte da outra metade de <c>montarUnidadesCruzadas</c> — a que roda a cada tentativa:
    /// consome o pareamento já pronto (<paramref name="candidatosCruzados"/>, do mais econômico
    /// pro menos), casando cópia com cópia enquanto sobrar das duas pontas de cada par. O que
    /// sobrar sem parceiro sai como peça solta — nunca pior do que já ficaria na receita "solta"
    /// ao lado dela. O casamento em si é sempre o mesmo, mas a SEQUÊNCIA em que as unidades
    /// resultantes são visitadas respeita <paramref name="ordem"/> (posição = menor
    /// índice-ordem entre os itens da unidade), pra embaralhar `ordem` entre tentativas
    /// continuar mudando o resultado.
    /// </summary>
    private static List<UnidadeContorno> MontarUnidadesCruzadas(
        IReadOnlyList<int> ordem, IReadOnlyList<ItemContorno> itens, IReadOnlyList<ParCruzado> candidatosCruzados)
    {
        if (candidatosCruzados.Count == 0)
            return [.. ordem.Select(i => new UnidadeContorno([i], null))];

        var porFormato = new Dictionary<string, Queue<int>>();
        foreach (var indice in ordem)
        {
            var formato = itens[indice].Item.Id.Split('#')[0];
            if (!porFormato.TryGetValue(formato, out var fila))
            {
                fila = new Queue<int>();
                porFormato[formato] = fila;
            }
            fila.Enqueue(indice);
        }

        var unidades = new List<UnidadeContorno>();
        foreach (var par in candidatosCruzados)
        {
            if (!porFormato.TryGetValue(par.FormatoA, out var filaA) || !porFormato.TryGetValue(par.FormatoB, out var filaB))
                continue;

            while (filaA.Count > 0 && filaB.Count > 0)
                unidades.Add(new UnidadeContorno([filaA.Dequeue(), filaB.Dequeue()], par.Formas));
        }

        foreach (var fila in porFormato.Values)
            while (fila.Count > 0)
                unidades.Add(new UnidadeContorno([fila.Dequeue()], null));

        var posicaoNoOrdem = new Dictionary<int, int>();
        for (var i = 0; i < ordem.Count; i++) posicaoNoOrdem[ordem[i]] = i;

        return [.. unidades.OrderBy(u => u.Itens.Min(indice => posicaoNoOrdem[indice]))];
    }

    private static ResultadoMotor ExecutarRetangulo(Receita receita, IReadOnlyList<int> ordem, IReadOnlyList<ItemCaixa> itens, double larguraTecidoCm, double margemCm)
    {
        var heuristica = receita.HeuristicaCaixa!.Value;
        var itensOrdenados = ordem.Select(i => (itens[i].Item, itens[i].PermiteDeitar)).ToList();

        var resultado = EncaixePorCaixaExecutor.Encaixar(larguraTecidoCm, itensOrdenados, heuristica);

        var posicoes = resultado.Posicoes
            .Select(p => new ItemDeResultado(p.ItemId, p.X, p.Y, p.Largura, p.Altura, p.Deitada ? 90 : 0))
            .ToList();

        var consumo = resultado.FundoMaximoCm + margemCm * 2;
        return new ResultadoMotor(consumo, resultado.ItensNaoEncaixados.Count, posicoes, resultado.ItensNaoEncaixados, resultado.AreaRealCm2);
    }

    /// <summary>
    /// Porte de <c>encaixarPorNFP</c> no despacho de receitas (§11.5/§11.7). Gira a peça em cada
    /// rotação permitida pelo giro dela (§11.11, portado 02/09/2026 — ver construção de
    /// <c>itensNfp</c> em <see cref="BuscarMelhorEncaixeAsync"/>) e ainda não tem agrupamento em
    /// bloco — cada item é sua própria unidade, colocado pela ordem sorteada da tentativa.
    /// </summary>
    /// <remarks>
    /// "Regra de segurança" do guia de melhorias (§7): a posição que sai do <see cref="EncaixadorPorNfp"/>
    /// (validada só por matemática de polígono contínuo) passa por uma SEGUNDA checagem
    /// independente — grade discreta, <see cref="ValidadorDeSobreposicaoNfp"/> — antes de
    /// virar resultado. Se as duas discordarem (sobreposição real, mesmo que rara), a
    /// tentativa inteira é desqualificada (consumo infinito, nada assentado) em vez de arriscar
    /// devolver posições sobrepostas — nunca pode ganhar a disputa contra as outras receitas.
    /// </remarks>
    private static ResultadoMotor ExecutarNfp(
        IReadOnlyList<int> ordem, IReadOnlyList<ItemParaNfp> itensNfp,
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, IReadOnlyList<PontoXY>>> contornosOriginaisPorRotacaoPorId,
        double larguraTecidoCm, double margemCm, Grade grade)
    {
        // Posiciona com o contorno RESIMPLIFICADO (mais leve pro NFP, ver comentário na
        // montagem de itensNfp) — mas a checagem de segurança e o resultado final usam o
        // contorno ORIGINAL, NA ROTAÇÃO ESCOLHIDA (§11.11), nunca a versão resimplificada.
        var ordenados = ordem.Select(i => itensNfp[i]).ToList();
        var resultado = EncaixadorPorNfp.Encaixar(larguraTecidoCm, ordenados);

        var posicionados = resultado.Posicoes
            .Select(p => (Contorno: contornosOriginaisPorRotacaoPorId[p.ItemId][p.RotacaoGraus], p.X, p.Y))
            .ToList();

        if (!ValidadorDeSobreposicaoNfp.SemSobreposicao(posicionados, grade))
        {
            var todosOsIds = ordenados.Select(i => i.Id).ToList();
            return new ResultadoMotor(double.PositiveInfinity, todosOsIds.Count, [], todosOsIds, 0);
        }

        var posicoes = resultado.Posicoes
            .Select(p =>
            {
                var contorno = contornosOriginaisPorRotacaoPorId[p.ItemId][p.RotacaoGraus];
                var caixa = Geometria.CaixaDeContorno(contorno);
                return new ItemDeResultado(p.ItemId, p.X + caixa.MinX, p.Y + caixa.MinY, caixa.Largura, caixa.Altura, p.RotacaoGraus);
            })
            .ToList();

        var areaReal = resultado.Posicoes.Sum(p => Math.Abs(Geometria.AreaComSinal(contornosOriginaisPorRotacaoPorId[p.ItemId][p.RotacaoGraus])));
        var consumo = resultado.FundoMaximo + margemCm * 2;

        return new ResultadoMotor(consumo, resultado.ItensNaoEncaixados.Count, posicoes, resultado.ItensNaoEncaixados, areaReal);
    }

    private static string DescreverReceita(Receita r) => r.Motor switch
    {
        MotorDeEncaixe.Contorno => $"{r.Motor}·{r.Agrupamento}·{r.Ordem}·{r.HeuristicaContorno}",
        MotorDeEncaixe.Nfp => $"{r.Motor}·{r.Ordem}",
        MotorDeEncaixe.Vaos => $"{r.Motor}·{r.Ordem}",
        _ => $"{r.Motor}·{r.Agrupamento}·{r.Ordem}·{r.HeuristicaCaixa}",
    };
}
