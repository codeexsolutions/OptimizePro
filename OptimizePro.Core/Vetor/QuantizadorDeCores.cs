namespace OptimizePro.Core.Vetor;

public readonly record struct CorRgb(byte R, byte G, byte B);

public sealed record OpcoesDeQuantizacao(int NumeroDeCores, double JuntarSombras = 0);

/// <summary>Índice -1 = pixel transparente (sem cor de paleta).</summary>
public sealed record ResultadoDaQuantizacao(IReadOnlyList<CorRgb> Paleta, IReadOnlyList<int> IndicesPorPixel);

/// <summary>
/// Porte de <c>juntarCores</c> (§14.1 passo 1, §14.2) — median cut (corta pelo peso em
/// pixels, não pela quantidade de cores distintas) seguido de relaxamento tipo Lloyd.
/// </summary>
public static class QuantizadorDeCores
{
    private const int MaximoDePassadasDeRelaxamento = 8;
    private const double LimiarDeParadaDoRelaxamento = 0.5;

    public static ResultadoDaQuantizacao Quantizar(ImagemRgba imagem, OpcoesDeQuantizacao opcoes)
    {
        if (opcoes.NumeroDeCores < 1)
            throw new ArgumentOutOfRangeException(nameof(opcoes), "NumeroDeCores precisa ser ≥1.");

        var histograma = HistogramaDeCores.Construir(imagem);

        if (histograma.Count == 0)
            return new ResultadoDaQuantizacao([], new int[imagem.Largura * imagem.Altura].Select(_ => -1).ToList());

        var caixas = MedianCut(histograma, opcoes.NumeroDeCores, opcoes.JuntarSombras);
        var paleta = caixas.Select(CorMediaDaCaixa).ToList();

        paleta = RelaxamentoDeLloyd(histograma, paleta, opcoes.JuntarSombras);

        var indices = AtribuirIndicesPorPixel(imagem, paleta, opcoes.JuntarSombras);

        return new ResultadoDaQuantizacao(paleta, indices);
    }

    /// <summary>
    /// Estima quantas cores a imagem "de verdade" tem — pra sugerir um bom valor inicial de
    /// "Cores" antes do usuário mexer em nada. Achado real (02/09/2026): com poucas cores
    /// (ex.: preset "Chapada", 4), o median cut é OBRIGADO a misturar regiões visualmente
    /// diferentes (ex.: um tom claro de um degradê com o branco de um texto) na mesma
    /// camada — o resultado sai com "vazamento" de cor entre regiões que nada tem a ver uma
    /// com a outra. Não é bug do quantizador, é o usuário não saber de antemão que aquela
    /// imagem precisa de mais baldes de cor. Agrupamento guloso simples por distância de cor
    /// (não é median cut de novo, só uma contagem aproximada) sobre o histograma já
    /// RGB555-quantizado de <see cref="HistogramaDeCores"/>: cores maiores que
    /// <paramref name="distanciaMinima"/> uma da outra (distância euclidiana em RGB) viram
    /// clusters separados; clusters com peso abaixo de <paramref name="pesoMinimoFracao"/> do
    /// total (provavelmente ruído de anti-aliasing de borda, não uma cor "de verdade") são
    /// descartados antes de contar.
    /// </summary>
    public static int SugerirNumeroDeCores(
        ImagemRgba imagem, int minimo = 2, int maximo = 16, double distanciaMinima = 45, double pesoMinimoFracao = 0.004)
    {
        var histograma = HistogramaDeCores.Construir(imagem);
        if (histograma.Count == 0)
            return minimo;

        var pesoTotal = histograma.Sum(e => e.Peso);
        var pesoMinimo = pesoTotal * pesoMinimoFracao;

        var representantes = new List<(double R, double G, double B, long Peso)>();
        foreach (var entrada in histograma.OrderByDescending(e => e.Peso))
        {
            var pertenceAExistente = false;
            for (var i = 0; i < representantes.Count; i++)
            {
                var r = representantes[i];
                var distancia = Math.Sqrt(Math.Pow(entrada.R - r.R, 2) + Math.Pow(entrada.G - r.G, 2) + Math.Pow(entrada.B - r.B, 2));
                if (distancia <= distanciaMinima)
                {
                    representantes[i] = (r.R, r.G, r.B, r.Peso + entrada.Peso);
                    pertenceAExistente = true;
                    break;
                }
            }

            if (!pertenceAExistente)
                representantes.Add((entrada.R, entrada.G, entrada.B, entrada.Peso));
        }

        var contagem = representantes.Count(r => r.Peso >= pesoMinimo);
        return Math.Clamp(contagem, minimo, maximo);
    }

    private sealed class CaixaDeCores
    {
        public required List<EntradaDeHistograma> Entradas { get; init; }
        public long PesoTotal => Entradas.Sum(e => e.Peso);
    }

    private static List<CaixaDeCores> MedianCut(IReadOnlyList<EntradaDeHistograma> histograma, int numeroDeCores, double juntarSombras)
    {
        var caixas = new List<CaixaDeCores> { new() { Entradas = [.. histograma] } };

        while (caixas.Count < numeroDeCores)
        {
            var indice = EscolherCaixaParaDividir(caixas);
            if (indice < 0)
                break; // nenhuma caixa tem mais de 1 entrada — não dá pra dividir mais

            var (a, b) = Dividir(caixas[indice], juntarSombras);
            caixas.RemoveAt(indice);
            caixas.Add(a);
            caixas.Add(b);
        }

        return caixas;
    }

    /// <summary>Prioriza dividir a caixa de maior peso total (mais pixels representados).</summary>
    private static int EscolherCaixaParaDividir(List<CaixaDeCores> caixas)
    {
        var melhorIndice = -1;
        var melhorPeso = -1L;

        for (var i = 0; i < caixas.Count; i++)
        {
            if (caixas[i].Entradas.Count < 2)
                continue;
            if (caixas[i].PesoTotal > melhorPeso)
            {
                melhorPeso = caixas[i].PesoTotal;
                melhorIndice = i;
            }
        }

        return melhorIndice;
    }

    private static (CaixaDeCores A, CaixaDeCores B) Dividir(CaixaDeCores caixa, double juntarSombras)
    {
        (double P0, double P1, double P2) Posicao(EntradaDeHistograma e) => EspacoDeAgrupamento.PosicaoDaCor(e.R, e.G, e.B, juntarSombras);

        var posicoes = caixa.Entradas.Select(Posicao).ToList();
        var amplitudeP0 = posicoes.Max(p => p.P0) - posicoes.Min(p => p.P0);
        var amplitudeP1 = posicoes.Max(p => p.P1) - posicoes.Min(p => p.P1);
        var amplitudeP2 = posicoes.Max(p => p.P2) - posicoes.Min(p => p.P2);

        Func<EntradaDeHistograma, double> chave =
            amplitudeP0 >= amplitudeP1 && amplitudeP0 >= amplitudeP2 ? e => Posicao(e).P0 :
            amplitudeP1 >= amplitudeP2 ? e => Posicao(e).P1 :
            e => Posicao(e).P2;

        var ordenadas = caixa.Entradas.OrderBy(chave).ToList();
        var metade = caixa.PesoTotal / 2.0;

        var indiceDeCorte = ordenadas.Count - 2; // fallback defensivo
        long acumulado = 0;
        for (var i = 0; i < ordenadas.Count; i++)
        {
            acumulado += ordenadas[i].Peso;
            if (acumulado >= metade)
            {
                indiceDeCorte = i;
                break;
            }
        }

        indiceDeCorte = Math.Clamp(indiceDeCorte, 0, ordenadas.Count - 2);

        return (
            new CaixaDeCores { Entradas = ordenadas[..(indiceDeCorte + 1)] },
            new CaixaDeCores { Entradas = ordenadas[(indiceDeCorte + 1)..] });
    }

    private static CorRgb CorMediaDaCaixa(CaixaDeCores caixa)
    {
        var pesoTotal = caixa.PesoTotal;
        var r = caixa.Entradas.Sum(e => e.R * e.Peso) / pesoTotal;
        var g = caixa.Entradas.Sum(e => e.G * e.Peso) / pesoTotal;
        var b = caixa.Entradas.Sum(e => e.B * e.Peso) / pesoTotal;

        return new CorRgb((byte)Math.Round(r), (byte)Math.Round(g), (byte)Math.Round(b));
    }

    private static List<CorRgb> RelaxamentoDeLloyd(IReadOnlyList<EntradaDeHistograma> histograma, List<CorRgb> paletaInicial, double juntarSombras)
    {
        var paleta = paletaInicial.ToList();

        for (var passada = 0; passada < MaximoDePassadasDeRelaxamento; passada++)
        {
            var somaR = new double[paleta.Count];
            var somaG = new double[paleta.Count];
            var somaB = new double[paleta.Count];
            var pesoTotal = new double[paleta.Count];

            foreach (var entrada in histograma)
            {
                var i = IndiceDaCorMaisProxima(entrada.R, entrada.G, entrada.B, paleta, juntarSombras);
                somaR[i] += entrada.R * entrada.Peso;
                somaG[i] += entrada.G * entrada.Peso;
                somaB[i] += entrada.B * entrada.Peso;
                pesoTotal[i] += entrada.Peso;
            }

            var maiorMovimento = 0.0;
            for (var i = 0; i < paleta.Count; i++)
            {
                if (pesoTotal[i] <= 0)
                    continue; // nenhuma entrada ficou mais perto desta cor nesta passada — mantém

                var nova = new CorRgb(
                    (byte)Math.Round(somaR[i] / pesoTotal[i]),
                    (byte)Math.Round(somaG[i] / pesoTotal[i]),
                    (byte)Math.Round(somaB[i] / pesoTotal[i]));

                var movimento = Math.Sqrt(
                    Math.Pow(nova.R - paleta[i].R, 2) + Math.Pow(nova.G - paleta[i].G, 2) + Math.Pow(nova.B - paleta[i].B, 2));

                if (movimento > maiorMovimento)
                    maiorMovimento = movimento;

                paleta[i] = nova;
            }

            if (maiorMovimento < LimiarDeParadaDoRelaxamento)
                break;
        }

        return paleta;
    }

    private static int IndiceDaCorMaisProxima(double r, double g, double b, List<CorRgb> paleta, double juntarSombras)
    {
        var posicao = EspacoDeAgrupamento.PosicaoDaCor(r, g, b, juntarSombras);

        var melhorIndice = 0;
        var melhorDistancia = double.MaxValue;

        for (var i = 0; i < paleta.Count; i++)
        {
            var posicaoPaleta = EspacoDeAgrupamento.PosicaoDaCor(paleta[i].R, paleta[i].G, paleta[i].B, juntarSombras);
            var d = Math.Pow(posicao.P0 - posicaoPaleta.P0, 2) + Math.Pow(posicao.P1 - posicaoPaleta.P1, 2) + Math.Pow(posicao.P2 - posicaoPaleta.P2, 2);

            if (d < melhorDistancia)
            {
                melhorDistancia = d;
                melhorIndice = i;
            }
        }

        return melhorIndice;
    }

    private static int[] AtribuirIndicesPorPixel(ImagemRgba imagem, List<CorRgb> paleta, double juntarSombras)
    {
        var indices = new int[imagem.Largura * imagem.Altura];

        for (var y = 0; y < imagem.Altura; y++)
        {
            for (var x = 0; x < imagem.Largura; x++)
            {
                var (r, g, b, a) = imagem.ObterPixel(x, y);
                indices[y * imagem.Largura + x] = a == 0 ? -1 : IndiceDaCorMaisProxima(r, g, b, paleta, juntarSombras);
            }
        }

        return indices;
    }
}
