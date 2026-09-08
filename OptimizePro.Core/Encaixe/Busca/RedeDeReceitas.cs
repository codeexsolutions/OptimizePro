namespace OptimizePro.Core.Encaixe.Busca;

/// <summary>Uma peça resumida no que a rede das receitas precisa saber sobre "o formato do trabalho" (§12.1) — ocupação da caixa delimitadora, dimensões e giro.</summary>
public sealed record PecaParaRede(double Ocupacao, double Largura, double Altura, TipoDeGiro Giro);

/// <summary>
/// O vocabulário de cada campo de uma receita (§12.1) — cobre tudo que os quatro
/// encaixadores do sistema original usam, incluindo combinações que este port ainda não
/// dispara (dupla/trio/quarteto/cruzada, faixas, NFP) — mantido completo por fidelidade
/// dimensional: se um dia pesos treinados no sistema original forem importados, a forma de
/// entrada da rede precisa bater exatamente.
/// </summary>
public static class VocabularioDeReceita
{
    public static readonly string[] Motores = ["contorno", "retangulo", "faixas", "nfp"];
    public static readonly string[] Agrupamentos = ["solta", "dupla", "trio", "quarteto", "cruzada", "deitada", "empe"];
    public static readonly string[] Ordens = ["area", "altura", "lado", "largura"];
    public static readonly string[] Heuristicas = ["fundo", "vazio", "bl", "bssf", "blsf", "baf", "encosta"];

    public const int Dimensao = 4 + 7 + 4 + 7; // 22

    /// <summary>
    /// Nosso <see cref="Receita"/> não modela "empe"/"deitada" como agrupamento do motor
    /// retângulo (a escolha é feita por item, em tempo de execução — ver
    /// <c>EncaixeService.ExecutarRetangulo</c>), diferente do sistema original, que gera as
    /// duas como receitas distintas quando alguma peça aceita giro livre. Mapeado sempre pra
    /// "empe" — simplificação documentada; "deitada" fica pra quando o dispatcher passar a
    /// tratar isso como duas receitas.
    /// </summary>
    public static string ChaveDaReceita(Receita r) => r.Motor switch
    {
        MotorDeEncaixe.Contorno => $"contorno/{Minusculo(r.Agrupamento)}/{Minusculo(r.Ordem)}/{Minusculo(r.HeuristicaContorno!.Value)}",
        MotorDeEncaixe.Faixas => $"faixas/{Minusculo(r.Agrupamento)}/{Minusculo(r.Ordem)}/{Minusculo(r.HeuristicaContorno!.Value)}",
        MotorDeEncaixe.Retangulo => $"retangulo/empe/{Minusculo(r.Ordem)}/{Minusculo(r.HeuristicaCaixa!.Value)}",
        MotorDeEncaixe.Nfp => "nfp/solta/area/encosta",
        _ => throw new ArgumentOutOfRangeException(nameof(r)),
    };

    private static string Minusculo<T>(T valor) where T : notnull => valor.ToString()!.ToLowerInvariant();

    public static double[] VetorDaChave(string chave)
    {
        var partes = chave.Split('/');
        if (partes.Length != 4)
            throw new ArgumentException($"Chave de receita mal formada: '{chave}'.", nameof(chave));

        var (motor, agrupamento, ordem, heuristica) = (partes[0], partes[1], partes[2], partes[3]);

        var vetor = new double[Dimensao];
        var i = 0;
        foreach (var m in Motores) vetor[i++] = m == motor ? 1 : 0;
        foreach (var a in Agrupamentos) vetor[i++] = a == agrupamento ? 1 : 0;
        foreach (var o in Ordens) vetor[i++] = o == ordem ? 1 : 0;
        foreach (var h in Heuristicas) vetor[i++] = h == heuristica ? 1 : 0;
        return vetor;
    }

    public static double[] VetorDaReceita(Receita r) => VetorDaChave(ChaveDaReceita(r));
}

/// <summary>Porte de <c>vetorDoTrabalho</c> (§12.1) — resume a lista de peças em 12 números: quantidade, largura do rolo, e estatísticas (média/desvio/mín/máx) de ocupação e proporção.</summary>
public static class VetorizacaoDoTrabalho
{
    public const int Dimensao = 12;

    private readonly record struct Estatisticas(double Media, double Desvio, double Min, double Max);

    private static Estatisticas Calcular(IReadOnlyList<double> lista)
    {
        if (lista.Count == 0) return new Estatisticas(0, 0, 0, 0);

        var media = lista.Sum() / lista.Count;
        var variancia = lista.Sum(v => (v - media) * (v - media)) / lista.Count;
        return new Estatisticas(media, Math.Sqrt(variancia), lista.Min(), lista.Max());
    }

    public static double[] VetorDoTrabalho(IReadOnlyList<PecaParaRede> pecas, double larguraTecido)
    {
        var ocupacoes = pecas.Select(p => p.Ocupacao).ToList();
        var proporcoes = pecas.Select(p => Math.Log2(p.Altura > 0 ? p.Largura / p.Altura : 1)).ToList();
        var oc = Calcular(ocupacoes);
        var pr = Calcular(proporcoes);
        var livres = pecas.Count(p => p.Giro == TipoDeGiro.Livre);
        var fixas = pecas.Count(p => p.Giro == TipoDeGiro.Fixa);
        var n = Math.Max(1, pecas.Count);

        return
        [
            Math.Log2(1 + pecas.Count) / 6,
            Math.Min(2, larguraTecido / 300),
            oc.Media, oc.Desvio, oc.Min, oc.Max,
            pr.Media / 3, pr.Desvio / 3,
            Math.Max(-1, Math.Min(1, pr.Min / 3)),
            Math.Max(-1, Math.Min(1, pr.Max / 3)),
            (double)livres / n,
            (double)fixas / n,
        ];
    }
}

/// <summary>Porte de <c>assinaturaDoTrabalho</c> (§11.12) — agrupa trabalhos por FORMA (ocupação da caixa + proporção), não por nome/quantidade; mesma matéria-prima de <see cref="VetorizacaoDoTrabalho"/>, só que arredondada pra caber num texto de balde.</summary>
public static class AssinaturaDeTrabalho
{
    public static string Calcular(IReadOnlyList<PecaParaRede> pecas, double larguraTecido)
    {
        var formatos = pecas
            .Select(p => $"{Arredondar(p.Ocupacao * 10)}:{Arredondar(Math.Log2(p.Altura > 0 ? p.Largura / p.Altura : 1) * 2)}")
            .OrderBy(s => s, StringComparer.Ordinal);

        return $"l{Arredondar(larguraTecido / 10)}|{string.Join(",", formatos)}";
    }

    private static long Arredondar(double v) => (long)Math.Round(v, MidpointRounding.AwayFromZero);
}

public sealed class CamadaDaRede
{
    public double[][] Pesos { get; set; } = [];
    public double[] Vies { get; set; } = [];
}

/// <summary>Uma rede densa feed-forward — pesos e vieses por camada, mais os tamanhos originais (§12.1).</summary>
public sealed class RedeNeural
{
    public int[] Tamanhos { get; set; } = [];
    public List<CamadaDaRede> Camadas { get; set; } = [];
}

/// <summary>
/// Porte de <c>encaixe-rede.js</c> (§12.1) — rede densa pequena, sem biblioteca nenhuma
/// (retropropagação manual), que prevê a chance de uma receita ganhar um trabalho.
/// Complementa a memória por assinatura exata (§12): generaliza pra trabalho PARECIDO, não
/// só idêntico.
/// </summary>
public static class RedeDeReceitas
{
    public const int DimensaoDeEntrada = VetorizacaoDoTrabalho.Dimensao + VocabularioDeReceita.Dimensao; // 34

    /// <summary>Pesos pequenos e aleatórios (escala de Xavier — menos chance de saturar tanh/sigmoide logo de cara); viés começa em zero.</summary>
    public static RedeNeural CriarRede(int[] tamanhos, Random aleatorio)
    {
        var camadas = new List<CamadaDaRede>();

        for (var i = 0; i < tamanhos.Length - 1; i++)
        {
            var entrada = tamanhos[i];
            var saida = tamanhos[i + 1];
            var escala = Math.Sqrt(2.0 / (entrada + saida));

            var pesos = new double[saida][];
            for (var j = 0; j < saida; j++)
            {
                pesos[j] = new double[entrada];
                for (var k = 0; k < entrada; k++)
                    pesos[j][k] = (aleatorio.NextDouble() * 2 - 1) * escala;
            }

            camadas.Add(new CamadaDaRede { Pesos = pesos, Vies = new double[saida] });
        }

        return new RedeNeural { Tamanhos = tamanhos, Camadas = camadas };
    }

    private static double Sigmoide(double x) => 1.0 / (1.0 + Math.Exp(-x));

    /// <summary>O passe pra frente, guardando a ativação de cada camada — o treino precisa disso pra retropropagação; a previsão pura usa só a última.</summary>
    private static List<double[]> PasseParaFrente(RedeNeural rede, double[] entrada)
    {
        var ativacoes = new List<double[]> { entrada };

        for (var i = 0; i < rede.Camadas.Count; i++)
        {
            var camada = rede.Camadas[i];
            var ehUltima = i == rede.Camadas.Count - 1;
            var anterior = ativacoes[^1];
            var saida = new double[camada.Pesos.Length];

            for (var j = 0; j < camada.Pesos.Length; j++)
            {
                var soma = camada.Vies[j];
                var linha = camada.Pesos[j];
                for (var k = 0; k < linha.Length; k++) soma += linha[k] * anterior[k];
                saida[j] = ehUltima ? Sigmoide(soma) : Math.Tanh(soma);
            }

            ativacoes.Add(saida);
        }

        return ativacoes;
    }

    /// <summary>A previsão: a chance (0 a 1) desta receita ganhar este trabalho, segundo a rede.</summary>
    public static double Prever(RedeNeural rede, double[] entrada) => PasseParaFrente(rede, entrada)[^1][0];

    /// <summary>
    /// Um passo de treino sobre UM exemplo: retropropagação com gradiente descendente simples.
    /// <paramref name="alvo"/> é 0 ou 1. Como a última camada é sigmoide e o erro é entropia
    /// cruzada, o gradiente na pré-ativação da saída simplifica pra <c>saída - alvo</c>.
    /// </summary>
    private static void PassoDeTreino(RedeNeural rede, double[] entrada, double alvo, double taxa)
    {
        var ativacoes = PasseParaFrente(rede, entrada);
        var nCamadas = rede.Camadas.Count;
        var delta = new[] { ativacoes[nCamadas][0] - alvo };

        for (var i = nCamadas - 1; i >= 0; i--)
        {
            var camada = rede.Camadas[i];
            var entradaDaCamada = ativacoes[i];
            var deltaAnterior = new double[entradaDaCamada.Length];

            for (var j = 0; j < camada.Pesos.Length; j++)
            {
                var linha = camada.Pesos[j];
                var d = delta[j];
                for (var k = 0; k < linha.Length; k++)
                {
                    // Acumula o delta da camada anterior com o peso de ANTES de mexer nele.
                    deltaAnterior[k] += d * linha[k];
                    linha[k] -= taxa * d * entradaDaCamada[k];
                }
                camada.Vies[j] -= taxa * d;
            }

            if (i > 0)
            {
                // A camada anterior usa tanh: a derivada dela, em função da própria ativação
                // (já calculada no passe pra frente), é 1 - ativação².
                var proximoDelta = new double[deltaAnterior.Length];
                for (var k = 0; k < deltaAnterior.Length; k++)
                    proximoDelta[k] = deltaAnterior[k] * (1 - ativacoes[i][k] * ativacoes[i][k]);
                delta = proximoDelta;
            }
        }
    }

    public sealed record ExemploDeTreino(double[] Entrada, double Alvo);

    /// <summary>Treina sobre uma lista de exemplos, embaralhando a ordem a cada época — sem isso a rede aprenderia um pouco a ordem dos dados, não só o padrão deles.</summary>
    public static void TreinarRede(RedeNeural rede, IReadOnlyList<ExemploDeTreino> exemplos, Random aleatorio, int epocas = 150, double taxa = 0.05)
    {
        var ordem = Enumerable.Range(0, exemplos.Count).ToArray();

        for (var e = 0; e < epocas; e++)
        {
            for (var i = ordem.Length - 1; i > 0; i--)
            {
                var j = aleatorio.Next(i + 1);
                (ordem[i], ordem[j]) = (ordem[j], ordem[i]);
            }

            foreach (var idx in ordem)
                PassoDeTreino(rede, exemplos[idx].Entrada, exemplos[idx].Alvo, taxa);
        }
    }

    /// <summary>Pontua um lote de receitas (pelas chaves) de uma vez, pra busca usar.</summary>
    public static Dictionary<string, double> PontuarReceitas(RedeNeural rede, double[] vetorTrabalho, IEnumerable<string> chaves)
    {
        var pontos = new Dictionary<string, double>();

        foreach (var chave in chaves)
        {
            if (pontos.ContainsKey(chave)) continue;

            var entrada = new double[vetorTrabalho.Length + VocabularioDeReceita.Dimensao];
            vetorTrabalho.CopyTo(entrada, 0);
            VocabularioDeReceita.VetorDaChave(chave).CopyTo(entrada, vetorTrabalho.Length);

            pontos[chave] = Prever(rede, entrada);
        }

        return pontos;
    }
}
