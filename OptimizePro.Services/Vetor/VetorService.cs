using OptimizePro.Core;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Services.Vetor;

/// <summary>
/// Porte de <c>vetorizarImagem</c> (§14.1) — os 9 passos do pipeline, orquestrando as
/// peças puras de <c>OptimizePro.Core.Vetor</c>. "porTrechos=false" (tudo Bézier, sem
/// reta/arco) não está implementado — <see cref="MontadorDeCaminho"/> do Core sempre decide
/// reta/arco/Bézier por trecho; ficou pro próximo incremento se um modo "tudo curva" for
/// realmente necessário.
/// </summary>
public sealed class VetorService : IVetorService
{
    private readonly PotraceProcessoService _potrace;

    public VetorService() : this(new PotraceProcessoService()) { }

    internal VetorService(PotraceProcessoService potrace) => _potrace = potrace;

    public Task<ResultadoDeVetorizacao> VetorizarAsync(byte[] imagemBytes, OpcoesDeVetorizacao opcoes, CancellationToken ct = default) =>
        opcoes.UsarMotorExterno
            ? _potrace.VetorizarAsync(imagemBytes, opcoes, ct)
            : Task.Run(() => Vetorizar(imagemBytes, opcoes, ct), ct);

    public Task<int> SugerirNumeroDeCoresAsync(byte[] imagemBytes, CancellationToken ct = default) =>
        Task.Run(() => QuantizadorDeCores.SugerirNumeroDeCores(DecodificacaoDeImagem.Decodificar(imagemBytes)), ct);

    private static ResultadoDeVetorizacao Vetorizar(byte[] imagemBytes, OpcoesDeVetorizacao opcoes, CancellationToken ct)
    {
        var imagem = DecodificacaoDeImagem.Decodificar(imagemBytes);

        // 1) juntarCores — median cut + relaxamento.
        var quantizacao = QuantizadorDeCores.Quantizar(imagem, new OpcoesDeQuantizacao(opcoes.Cores, opcoes.JuntarSombras));

        // 2) limparCisco.
        var indicesLimpos = LimpezaDeCisco.Limpar(quantizacao.IndicesPorPixel, imagem.Largura, imagem.Altura, opcoes.Detalhe);

        var camadas = new List<ConversorSvg.CamadaSvg>();

        // Pinta as cores de MAIOR área primeiro — igual à referência (camadas.sort por
        // quantidade de pixels antes de montar o SVG). Sem isso, a ordem depende só de como o
        // median-cut/relaxamento numerou a paleta, e uma cor de área grande pode acabar sendo
        // desenhada DEPOIS (por cima) de traços finos de outra cor — cobrindo texto/detalhes.
        var indicesPorArea = OrdemDePinturaPorArea(indicesLimpos, quantizacao.Paleta.Count);

        foreach (var indice in indicesPorArea)
        {
            ct.ThrowIfCancellationRequested();

            // 3) contornosDoMapa — externos e furos desta cor.
            var contornos = ContornosDoMapa.Extrair(imagem.Largura, imagem.Altura, indicesLimpos, indice);
            if (contornos.Count == 0) continue;

            var corDeDentro = quantizacao.Paleta[indice];

            var caminhos = new List<CaminhoMontado>(contornos.Count);
            foreach (var contorno in contornos)
            {
                var pontos = contorno.Pontos;

                // 4) afinarNoSubpixel (opcional) — sonda a cor real de fora LOCALMENTE (ao
                // longo da normal de cada ponto), não uma média global da paleta.
                if (opcoes.Subpixel)
                    pontos = RefinamentoSubpixel.Afinar(pontos, imagem, corDeDentro);

                // Tolerância relativa ao tamanho do PRÓPRIO contorno — um traço fino (perna de
                // letra) não pode usar a mesma tolerância "solta" de um contorno grande, ou vira
                // mancha. Mesma ideia da referência (`ladoMenorDoContorno(bruto) * 0.16`).
                var ladoMenor = LadoMenorDoContorno(pontos);
                var toleranciaAjustada = Math.Min(opcoes.Suavidade, ladoMenor * 0.16);
                var parametrosDeRemontagem = new ParametrosDeRemontagem(
                    ToleranciaDeReta: toleranciaAjustada, ToleranciaDeArco: toleranciaAjustada, TensaoDeBezier: opcoes.Tensao);
                var toleranciaDeCirculo = Math.Max(0.8, toleranciaAjustada * 1.5);

                // 5) acharFormaRedonda — se passar, emite arco e pula simplificar/quinas/montar.
                var circulo = opcoes.Redondas ? DetectorDeFormaRedonda.Detectar(pontos, toleranciaDeCirculo) : null;

                caminhos.Add(circulo is { } c
                    ? CaminhoDoCirculo(c)
                    : MontarCaminhoPoligonal(pontos, opcoes.QuinaGraus, parametrosDeRemontagem));
            }

            // 9) por camada (cor), concatena externo+furos num só <path> (evenodd).
            var caminhoD = ConversorSvg.ParaComandoDePathComFuros(caminhos);
            if (caminhoD.Length > 0)
                camadas.Add(new ConversorSvg.CamadaSvg(caminhoD, corDeDentro));
        }

        var svg = ConversorSvg.MontarSvg(imagem.Largura, imagem.Altura, camadas);
        return new ResultadoDeVetorizacao(svg, imagem.Largura, imagem.Altura);
    }

    private static CaminhoMontado MontarCaminhoPoligonal(IReadOnlyList<PontoXY> pontos, double quinaGraus, ParametrosDeRemontagem parametros)
    {
        // 6) simplificar (Douglas-Peucker) — guarda também o índice original de cada ponto
        // mantido, pra "Montar" poder checar reta/arco contra o contorno BRUTO de verdade
        // (não só contra os poucos vértices que sobraram), evitando que recortes côncavos
        // finos (ex.: o garfo de um "Y") sejam arredondados por engano.
        var (simplificado, indicesNoBruto) = Geometria.SimplificarComIndices(pontos, parametros.ToleranciaDeReta);

        // 7) acharQuinas + tangentesDoContorno (dentro de Montar) — 8) remonta reta/arco/Bézier.
        return MontadorDeCaminho.Montar(simplificado, quinaGraus, parametros, pontos, indicesNoBruto);
    }

    private static CaminhoMontado CaminhoDoCirculo(CirculoAjustado circulo)
    {
        var inicio = new PontoXY(circulo.Centro.X - circulo.Raio, circulo.Centro.Y);
        var meio = new PontoXY(circulo.Centro.X + circulo.Raio, circulo.Centro.Y);

        return new CaminhoMontado(inicio,
        [
            new SegmentoArco(circulo.Raio, false, true, meio),
            new SegmentoArco(circulo.Raio, false, true, inicio),
        ]);
    }

    /// <summary>Índices da paleta ordenados por área (nº de pixels) decrescente — maior cor primeiro.</summary>
    private static IReadOnlyList<int> OrdemDePinturaPorArea(IReadOnlyList<int> indicesPorPixel, int numeroDeCores)
    {
        var contagem = new int[numeroDeCores];
        foreach (var indice in indicesPorPixel)
            contagem[indice]++;

        var ordem = new int[numeroDeCores];
        for (var i = 0; i < numeroDeCores; i++) ordem[i] = i;

        Array.Sort(ordem, (a, b) => contagem[b].CompareTo(contagem[a]));
        return ordem;
    }

    private static double LadoMenorDoContorno(IReadOnlyList<PontoXY> pontos)
    {
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach (var p in pontos)
        {
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        return Math.Min(maxX - minX, maxY - minY);
    }
}
