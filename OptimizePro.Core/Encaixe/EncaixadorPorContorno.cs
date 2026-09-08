using System.Numerics;

namespace OptimizePro.Core.Encaixe;

/// <summary>
/// <see cref="Contato"/> (02/09/2026, §9.3 — "heurística de perímetro de contato") é
/// experimental, fora do vocabulário do sistema original (<c>VocabularioDeReceita.Heuristicas</c>
/// documenta só as 7 heurísticas de lá — "contato" fica sem codificação própria na rede de
/// receitas de propósito, não é um esquecimento).
/// </summary>
public enum HeuristicaDeContorno { Fundo, Vazio, Contato }

public readonly record struct PosicaoEncontrada(int X, int Y, long P1, long P2);

public sealed record PosicaoDeItem(string ItemId, int RotacaoGraus, int X, int Y);

public sealed record ResultadoContorno(IReadOnlyList<PosicaoDeItem> Posicoes, IReadOnlyList<string> ItensNaoEncaixados, int FundoMaximo)
{
    /// <summary>Comprimento de tecido consumido (cm) — <c>fundoMax*passo + margem*2</c> (§11.3).</summary>
    public double CalcularConsumoCm(double passoCm, double margemCm) => FundoMaximo * passoCm + margemCm * 2;
}

/// <summary>
/// Porte de <c>melhorPosicaoDaUnidade</c>/laço externo de <c>encaixe-motor.js</c> (§11.3) —
/// o núcleo do sistema: bottom-left sobre o "relevo" (topo/base) de cada forma, nunca
/// sobrepondo porque <c>perfil</c> guarda o ponto mais baixo já ocupado em cada coluna.
/// </summary>
/// <remarks>
/// Sem as otimizações de performance da especificação original (poda por colunas-sonda,
/// varredura "pulando de 3 em 3") — a fórmula de pontuação e o resultado final são
/// idênticos, só mais lento (varre todo x, sempre coluna a coluna completa). Adicionar
/// essas podas depois se a performance real exigir; não afetam qual posição é escolhida.
/// </remarks>
public static class EncaixadorPorContorno
{
    /// <summary>Quantas colunas-sonda amostrar pra poda barata (§11.3 da especificação — "até 8"). Espalhadas por igual entre as colunas com relevo válido da forma.</summary>
    private const int MaximoDeSondas = 8;

    /// <summary>"pulo" da varredura grossa (§11.3 — "medido: pulo=3 é o ponto ótimo, 2,5-2,8× mais tentativas pelo mesmo custo"). 1 desliga (varredura exata, todo x).</summary>
    private const int SaltoXPadrao = 3;

    /// <summary>
    /// Acha a melhor posição x/y para <paramref name="forma"/> sobre o relevo atual do tecido.
    /// </summary>
    /// <remarks>
    /// Porta as duas podas que a especificação (§11.3) documentava como deliberadamente
    /// OMITIDAS até agora ("Adicionar essas podas depois se a performance real exigir") —
    /// motivo concreto pra portar agora: medido contra o projeto original em dados reais
    /// (25 arquivos/57 peças, mesma config exata, mesmo tempo de busca): o motor de contorno
    /// fazia só ~36 tentativas/s (1 tentativa completa ~28ms) contra a ordem de milhares/s do
    /// original, e "Retângulo" (que ignora silhueta, muito mais barato por natureza) sempre
    /// vencia por essa desvantagem de tentativas, não por ser genuinamente melhor pra essas
    /// peças. As duas podas, aplicadas juntas:
    /// (1) <b>poda por colunas-sonda</b>: antes de varrer TODAS as colunas de um x candidato,
    /// mede só até 8 colunas espalhadas — dá um limite inferior barato pra "quanto essa forma
    /// desceria aqui"; se já prova que não pode bater o melhor atual, descarta sem medir o
    /// resto. Só válida (matematicamente, sem arriscar trocar o resultado) pra heurística
    /// "fundo" — cresce estritamente com y, então um limite inferior de y já basta pra provar
    /// que a nota final também vai ser pior. "vazio" depende da soma de TODAS as colunas
    /// (não só das sondas), então mantém a varredura completa.
    /// (2) <b>corte precoce durante a varredura</b>: mesma lógica aplicada coluna a coluna
    /// enquanto mede de verdade — <c>y</c> só cresce (nunca diminui) à medida que a varredura
    /// avança, então assim que ele já prova que vai perder, para no meio sem terminar as
    /// colunas restantes.
    /// (3) <b>varredura "pulando de 3 em 3"</b> (<see cref="SaltoXPadrao"/>): em vez de testar
    /// TODO x, uma passada grossa de <c>pulo</c> em <c>pulo</c> (+ o extremo final) acha a
    /// região promissora, e só ali uma passada fina (±<c>pulo</c>-1) refina pro ótimo local.
    /// Reduz o número de x's avaliados de <c>xMax</c> pra ~<c>xMax/pulo</c>. Ao contrário das
    /// duas podas acima, esta é uma aproximação deliberada (pode perder um ótimo GLOBAL isolado
    /// fora da vizinhança do melhor achado na passada grossa) — o próprio original aceita essa
    /// troca (mede "2,5-2,8× mais tentativas pelo mesmo custo") porque mais tentativas de busca
    /// tende a valer mais que a precisão de uma posição isolada.
    /// </remarks>
    public static PosicaoEncontrada? MelhorPosicaoDaUnidade(IReadOnlyList<int> perfil, int colsTecido, Forma forma, HeuristicaDeContorno heuristica, int saltoX = SaltoXPadrao)
    {
        if (heuristica == HeuristicaDeContorno.Contato)
            return MelhorPosicaoPorContato(perfil, colsTecido, forma, saltoX);

        var usaVazio = heuristica == HeuristicaDeContorno.Vazio;
        var xMax = colsTecido - forma.Colunas;
        if (xMax < 0) return null;

        var sondas = ColunasSonda(forma);
        PosicaoEncontrada? melhor = null;

        PosicaoEncontrada? Avaliar(int x)
        {
            // Poda barata (1): limite inferior de y usando só as colunas-sonda — só serve pra
            // "fundo" (monótona em y); "vazio" precisa da soma exata de todas as colunas.
            if (!usaVazio && melhor is { } atual1 && sondas.Count > 0)
            {
                var piso = 0;
                foreach (var c in sondas)
                {
                    var encostaSonda = perfil[x + c] - forma.Topo[c];
                    if (encostaSonda > piso) piso = encostaSonda;
                }

                if (piso + forma.MaxBase + 1 > atual1.P1) return null;
            }

            var y = 0;
            long somaPerfil = 0;

            for (var c = 0; c < forma.Colunas; c++)
            {
                if (forma.Topo[c] < 0) continue;

                var altura = perfil[x + c];
                somaPerfil += altura;

                var encosta = altura - forma.Topo[c];
                if (encosta <= y) continue;
                y = encosta;

                // Corte precoce (2): y só cresce daqui em diante — se já perde, para.
                if (!usaVazio && melhor is { } atual2 && y + forma.MaxBase + 1 > atual2.P1) return null;
            }

            var vazio = y * (long)forma.NumeroDeColunasValidas + forma.SomaTopo - somaPerfil;
            var fundo = y + forma.MaxBase + 1;

            var p1 = usaVazio ? vazio : fundo;
            var p2 = usaVazio ? fundo : vazio;
            return new PosicaoEncontrada(x, y, p1, p2);
        }

        void Considerar(int x)
        {
            if (Avaliar(x) is not { } candidato) return;
            if (melhor is null || candidato.P1 < melhor.Value.P1 || (candidato.P1 == melhor.Value.P1 && candidato.P2 < melhor.Value.P2))
                melhor = candidato;
        }

        if (saltoX <= 1)
        {
            for (var x = 0; x <= xMax; x++) Considerar(x);
            return melhor;
        }

        // Varredura "pulando de 3 em 3" (3): passada grossa acha a região promissora...
        for (var x = 0; x <= xMax; x += saltoX) Considerar(x);
        if (xMax % saltoX != 0) Considerar(xMax);

        // ...passada fina refina ao redor do melhor local encontrado.
        if (melhor is { } m)
        {
            var inicio = Math.Max(0, m.X - (saltoX - 1));
            var fim = Math.Min(xMax, m.X + (saltoX - 1));
            for (var x = inicio; x <= fim; x++) Considerar(x);
        }

        return melhor;
    }

    /// <summary>
    /// Heurística experimental "perímetro de contato" (02/09/2026, §9.3) — técnica clássica de
    /// nesting que nem "fundo" (só altura) nem "vazio" (área morta acima da forma) capturam
    /// direto: em vez de julgar a posição pela altura ou pelo buraco que sobra, julga por
    /// QUANTAS colunas da forma ficam DE VERDADE encostadas no relevo já ocupado (sem gap),
    /// como um quebra-cabeça — mais colunas em contato tende a "grudar" formas côncavas
    /// (cava com cava, gola com gola) melhor que só minimizar altura.
    /// </summary>
    /// <remarks>
    /// Precisa saber o `y` final (o máximo de "encosta" entre as colunas) ANTES de poder
    /// contar quais colunas empataram com ele — por isso são duas passadas pela forma (achar
    /// y, depois contar contato), diferente do laço de uma passada só de
    /// <see cref="MelhorPosicaoDaUnidade"/>. Sem as podas 1/2 (colunas-sonda/corte precoce) —
    /// são específicas da monotonia de "fundo", não fazem sentido pra "quantas colunas
    /// empatam com o máximo"; mantém só a varredura "pulando de 3" (poda 3), que é
    /// heurística-agnóstica.
    /// </remarks>
    private static PosicaoEncontrada? MelhorPosicaoPorContato(IReadOnlyList<int> perfil, int colsTecido, Forma forma, int saltoX)
    {
        var xMax = colsTecido - forma.Colunas;
        if (xMax < 0) return null;

        PosicaoEncontrada? melhor = null;

        PosicaoEncontrada Avaliar(int x)
        {
            var y = 0;
            for (var c = 0; c < forma.Colunas; c++)
            {
                if (forma.Topo[c] < 0) continue;
                var encosta = perfil[x + c] - forma.Topo[c];
                if (encosta > y) y = encosta;
            }

            var colunasEmContato = 0;
            for (var c = 0; c < forma.Colunas; c++)
            {
                if (forma.Topo[c] < 0) continue;
                if (perfil[x + c] - forma.Topo[c] == y) colunasEmContato++;
            }

            var semContato = forma.NumeroDeColunasValidas - colunasEmContato; // minimizar = maximizar contato.
            var fundo = y + forma.MaxBase + 1; // desempate: entre duas posições com mesmo contato, a mais baixa.

            return new PosicaoEncontrada(x, y, semContato, fundo);
        }

        void Considerar(int x)
        {
            var candidato = Avaliar(x);
            if (melhor is null || candidato.P1 < melhor.Value.P1 || (candidato.P1 == melhor.Value.P1 && candidato.P2 < melhor.Value.P2))
                melhor = candidato;
        }

        if (saltoX <= 1)
        {
            for (var x = 0; x <= xMax; x++) Considerar(x);
            return melhor;
        }

        for (var x = 0; x <= xMax; x += saltoX) Considerar(x);
        if (xMax % saltoX != 0) Considerar(xMax);

        if (melhor is { } m)
        {
            var inicio = Math.Max(0, m.X - (saltoX - 1));
            var fim = Math.Min(xMax, m.X + (saltoX - 1));
            for (var x = inicio; x <= fim; x++) Considerar(x);
        }

        return melhor;
    }

    /// <summary>Até <see cref="MaximoDeSondas"/> índices de colunas com relevo válido (Topo≥0), espalhados por igual — usados só como limite inferior barato (poda 1 de <see cref="MelhorPosicaoDaUnidade"/>).</summary>
    private static IReadOnlyList<int> ColunasSonda(Forma forma)
    {
        var validas = new List<int>(forma.Colunas);
        for (var c = 0; c < forma.Colunas; c++)
            if (forma.Topo[c] >= 0) validas.Add(c);

        if (validas.Count <= MaximoDeSondas) return validas;

        var sondas = new List<int>(MaximoDeSondas);
        for (var i = 0; i < MaximoDeSondas; i++)
            sondas.Add(validas[i * (validas.Count - 1) / (MaximoDeSondas - 1)]);

        return sondas;
    }

    /// <summary>
    /// Como <see cref="MelhorPosicaoDaUnidade"/>, mas guarda até <paramref name="k"/> posições
    /// candidatas (não só a melhor) — porte do "top-K candidatos" do guia de melhorias de
    /// aproveitamento (§3.1): a escolha gulosa de uma única posição pode fechar o vão que a
    /// PRÓXIMA peça precisaria; guardar as K melhores permite comparar cada uma com o efeito
    /// que ela tem na peça seguinte (look-ahead, §3.3), em vez de decidir só pelo escore
    /// imediato desta peça.
    /// </summary>
    public static IReadOnlyList<PosicaoEncontrada> TopKPosicoes(IReadOnlyList<int> perfil, int colsTecido, Forma forma, HeuristicaDeContorno heuristica, int k)
    {
        var usaVazio = heuristica == HeuristicaDeContorno.Vazio;
        var candidatos = new List<PosicaoEncontrada>();

        var xMax = colsTecido - forma.Colunas;
        for (var x = 0; x <= xMax; x++)
        {
            var y = 0;
            long somaPerfil = 0;

            for (var c = 0; c < forma.Colunas; c++)
            {
                if (forma.Topo[c] < 0) continue;

                var altura = perfil[x + c];
                somaPerfil += altura;

                var encosta = altura - forma.Topo[c];
                if (encosta > y) y = encosta;
            }

            var vazio = y * (long)forma.NumeroDeColunasValidas + forma.SomaTopo - somaPerfil;
            var fundo = y + forma.MaxBase + 1;

            var p1 = usaVazio ? vazio : fundo;
            var p2 = usaVazio ? fundo : vazio;

            candidatos.Add(new PosicaoEncontrada(x, y, p1, p2));
        }

        return [.. candidatos.OrderBy(p => p.P1).ThenBy(p => p.P2).Take(Math.Max(1, k))];
    }

    /// <summary>
    /// Laço externo (§11.3): assenta cada unidade da <paramref name="ordem"/>, na ordem
    /// dada, sobre o relevo do tecido (<paramref name="colsTecido"/> colunas).
    /// </summary>
    public static ResultadoContorno Encaixar(
        int colsTecido, IReadOnlyList<(ItemEncaixe Item, int RotacaoGraus)> ordem, HeuristicaDeContorno heuristica)
    {
        var perfil = new int[colsTecido];
        var posicoes = new List<PosicaoDeItem>();
        var naoEncaixados = new List<string>();
        var fundoMaximo = 0;

        foreach (var (item, rotacao) in ordem)
        {
            if (!item.MascarasPorRotacao.TryGetValue(rotacao, out var mascara))
                throw new ArgumentException($"Item '{item.Id}' não tem máscara para a rotação {rotacao}°.");

            var forma = Forma.DeMascaraUnica(mascara);

            var escolha = forma.Colunas <= colsTecido
                ? MelhorPosicaoDaUnidade(perfil, colsTecido, forma, heuristica)
                : null;

            if (escolha is not { } pos)
            {
                naoEncaixados.Add(item.Id);
                continue;
            }

            for (var c = 0; c < forma.Colunas; c++)
            {
                if (forma.Topo[c] < 0) continue;
                perfil[pos.X + c] = pos.Y + forma.Base[c] + 1;
            }

            var fundoDaForma = pos.Y + forma.MaxBase + 1;
            if (fundoDaForma > fundoMaximo) fundoMaximo = fundoDaForma;

            posicoes.Add(new PosicaoDeItem(item.Id, rotacao, pos.X, pos.Y));
        }

        return new ResultadoContorno(posicoes, naoEncaixados, fundoMaximo);
    }

    /// <summary>
    /// V2 EXPERIMENTAL (02/09/2026, §9.3 — "vá fundo na reescrita do loop de pontuação").
    /// Isolada de propósito: se não render ganho real medido contra dados reais, a forma de
    /// reverter é simplesmente APAGAR este método (e o que o chama) — <see cref="MelhorPosicaoDaUnidade"/>
    /// original fica intocada, nunca editada.
    /// </summary>
    /// <remarks>
    /// Contexto: mesmo depois das podas (§11.3) e da correção do bug de reconstrução
    /// (§11.7), a busca converge (mais tempo de busca não muda o resultado — medido: 5min
    /// real deu o MESMO consumo que 60s) contra um concorrente de mercado (Audace) que ainda
    /// fica ~1,8% à frente num lote de produção real. Hipótese: o custo intrínseco do loop de
    /// pontuação (O(colsTecido/salto × formaColunas), coluna a coluna) é o teto — não falta
    /// tempo, falta LOOP MAIS BARATO pra caber mais tentativas no mesmo tempo.
    ///
    /// Mudança: processa <see cref="Vector{T}"/>.Count colunas de uma vez (SIMD real do
    /// hardware — 8 int32 por instrução em CPU com AVX2, por exemplo) em vez de uma coluna
    /// por chamada de loop. Mesma fórmula de pontuação, mesmo resultado matemático — só a
    /// FORMA de somar/achar o máximo que muda. Colunas inválidas (Topo&lt;0) são mascaradas
    /// (int.MinValue no vetor de "encosta", 0 no vetor de "perfil" pra soma) em vez de puladas
    /// uma a uma.
    ///
    /// Troca consciente: perde o "corte precoce" (poda 2 de <see cref="MelhorPosicaoDaUnidade"/>)
    /// — vetorizado, não dá pra abortar NO MEIO de um bloco de 8 colunas sem complicar a
    /// lógica de mascaramento; processa o bloco inteiro sempre. Mantém a poda por
    /// colunas-sonda (1, antes de começar) e a varredura "pulando de 3" (3) — só a poda 2 sai.
    /// Se vetorizar 8 colunas de cada vez for mais barato que a poda 2 economizava, ganha
    /// líquido; senão, perde. Só medição real decide — daí ser uma V2 isolada, não uma edição
    /// da V1.
    ///
    /// Exige que <paramref name="perfil"/>, <c>forma.Topo</c> e <c>forma.Base</c> sejam
    /// literalmente <c>int[]</c> por baixo do <c>IReadOnlyList&lt;int&gt;</c> (sempre são, na
    /// prática — <see cref="Mascara"/> e o pool de array em <c>EncaixeService</c> só usam
    /// arrays de verdade) — cai pra V1 sem erro se não for (nunca deveria acontecer).
    /// </remarks>
    public static PosicaoEncontrada? MelhorPosicaoDaUnidadeV2(IReadOnlyList<int> perfil, int colsTecido, Forma forma, HeuristicaDeContorno heuristica, int saltoX = SaltoXPadrao)
    {
        // "Contato" (experimental, nova) só está implementada na V1 por ora — a V2 não
        // precisa cobrir toda heurística pra ser útil, só cai de volta com segurança.
        if (heuristica == HeuristicaDeContorno.Contato)
            return MelhorPosicaoDaUnidade(perfil, colsTecido, forma, heuristica, saltoX);

        if (perfil is not int[] perfilArr || forma.Topo is not int[] topoArr || topoArr.Length < forma.Colunas)
            return MelhorPosicaoDaUnidade(perfil, colsTecido, forma, heuristica, saltoX);

        var usaVazio = heuristica == HeuristicaDeContorno.Vazio;
        var xMax = colsTecido - forma.Colunas;
        if (xMax < 0) return null;

        var sondas = ColunasSonda(forma);
        var colunas = forma.Colunas;
        var largura = Vector<int>.Count;
        var maxBase = forma.MaxBase;
        var numeroDeColunasValidas = forma.NumeroDeColunasValidas;
        var somaTopo = forma.SomaTopo;

        PosicaoEncontrada? melhor = null;

        PosicaoEncontrada? Avaliar(int x)
        {
            if (!usaVazio && melhor is { } atual1 && sondas.Count > 0)
            {
                var pisoSonda = 0;
                foreach (var c in sondas)
                {
                    var encostaSonda = perfilArr[x + c] - topoArr[c];
                    if (encostaSonda > pisoSonda) pisoSonda = encostaSonda;
                }

                if (pisoSonda + maxBase + 1 > atual1.P1) return null;
            }

            var y = 0;
            long somaPerfil = 0;
            var c2 = 0;

            if (colunas >= largura)
            {
                var maxVec = new Vector<int>(int.MinValue);
                var somaVec = Vector<int>.Zero;
                var zeroVec = Vector<int>.Zero;
                var minVec = new Vector<int>(int.MinValue);

                for (; c2 <= colunas - largura; c2 += largura)
                {
                    var perfilVec = new Vector<int>(perfilArr, x + c2);
                    var topoVec = new Vector<int>(topoArr, c2);
                    var validoMask = Vector.GreaterThanOrEqual(topoVec, zeroVec);

                    var encostaVec = perfilVec - topoVec;
                    maxVec = Vector.Max(maxVec, Vector.ConditionalSelect(validoMask, encostaVec, minVec));
                    somaVec += Vector.ConditionalSelect(validoMask, perfilVec, zeroVec);
                }

                for (var i = 0; i < largura; i++)
                {
                    if (maxVec[i] > y) y = maxVec[i];
                    somaPerfil += somaVec[i];
                }
            }

            for (var c = c2; c < colunas; c++)
            {
                if (topoArr[c] < 0) continue;
                var altura = perfilArr[x + c];
                somaPerfil += altura;
                var encosta = altura - topoArr[c];
                if (encosta > y) y = encosta;
            }

            var vazio = y * (long)numeroDeColunasValidas + somaTopo - somaPerfil;
            var fundo = y + maxBase + 1;

            var p1 = usaVazio ? vazio : fundo;
            var p2 = usaVazio ? fundo : vazio;
            return new PosicaoEncontrada(x, y, p1, p2);
        }

        void Considerar(int x)
        {
            if (Avaliar(x) is not { } candidato) return;
            if (melhor is null || candidato.P1 < melhor.Value.P1 || (candidato.P1 == melhor.Value.P1 && candidato.P2 < melhor.Value.P2))
                melhor = candidato;
        }

        if (saltoX <= 1)
        {
            for (var x = 0; x <= xMax; x++) Considerar(x);
            return melhor;
        }

        for (var x = 0; x <= xMax; x += saltoX) Considerar(x);
        if (xMax % saltoX != 0) Considerar(xMax);

        if (melhor is { } m)
        {
            var inicio = Math.Max(0, m.X - (saltoX - 1));
            var fim = Math.Min(xMax, m.X + (saltoX - 1));
            for (var x = inicio; x <= fim; x++) Considerar(x);
        }

        return melhor;
    }
}
