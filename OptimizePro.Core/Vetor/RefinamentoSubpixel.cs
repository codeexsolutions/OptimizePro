namespace OptimizePro.Core.Vetor;

/// <summary>
/// Porte de <c>afinarNoSubpixel</c> (§14.4) — para cada ponto do contorno (traçado na
/// quina das células, resolução inteira), sonda a cor real por interpolação bilinear e
/// projeta na reta entre a cor de dentro e a cor de fora, deslocando o ponto ao longo da
/// normal local em até ±0.5px na direção da borda real (anti-aliased).
/// </summary>
/// <remarks>
/// A "cor de fora" é sondada LOCALMENTE — uma amostra bilinear a <see cref="DistanciaDeSonda"/>
/// pixels pra fora, ao longo da normal de CADA ponto — em vez de uma média global das demais
/// cores da paleta. Com paleta de poucas cores (ex.: 6) numa imagem com gradiente, a média de
/// "todas as outras cores" quase nunca bate com o vizinho real de um trecho específico, o que
/// jogava o deslocamento calculado na direção/magnitude erradas bem onde mais importa (traços
/// finos de letra) — corrigido sondando o vizinho de verdade em vez de aproximar.
///
/// Simplificação consciente ainda restante frente à especificação original: não implementa o
/// "cache compartilhado entre camadas" (garantir que a borda entre duas cores adjacentes saia
/// com o mesmo ponto refinado dos dois lados) — cada camada refina seu próprio contorno
/// independentemente; se sair uma fresta visível entre camadas vizinhas no resultado final, é
/// o próximo lugar a revisar.
/// </remarks>
public static class RefinamentoSubpixel
{
    private const double DistanciaDeSonda = 1.5;

    public static IReadOnlyList<PontoXY> Afinar(IReadOnlyList<PontoXY> contorno, ImagemRgba imagem, CorRgb corDeDentro)
    {
        var resultado = new List<PontoXY>(contorno.Count);

        for (var i = 0; i < contorno.Count; i++)
        {
            var normal = NormalLocal(contorno, i);
            var deslocamento = CalcularDeslocamento(contorno[i], normal, imagem, corDeDentro);

            resultado.Add(new PontoXY(
                contorno[i].X + normal.X * deslocamento,
                contorno[i].Y + normal.Y * deslocamento));
        }

        return resultado;
    }

    /// <summary>
    /// Perpendicular à corda entre vizinhos, apontando pra fora — o contorno anda com o
    /// interior à direita do sentido de percurso (§14.3), então girar a tangente -90°
    /// (dy, -dx) aponta pra fora.
    /// </summary>
    private static PontoXY NormalLocal(IReadOnlyList<PontoXY> contorno, int i)
    {
        var n = contorno.Count;
        var anterior = contorno[(i - 1 + n) % n];
        var proximo = contorno[(i + 1) % n];

        var dx = proximo.X - anterior.X;
        var dy = proximo.Y - anterior.Y;
        var comprimento = Math.Sqrt(dx * dx + dy * dy);

        return comprimento < 1e-9 ? new PontoXY(0, 0) : new PontoXY(dy / comprimento, -dx / comprimento);
    }

    private static double CalcularDeslocamento(PontoXY ponto, PontoXY normal, ImagemRgba imagem, CorRgb corDeDentro)
    {
        // O contorno anda pelas QUINAS das células (§14.3) — a quina (c,l) fica exatamente
        // na fronteira compartilhada pelos pixels (c-1,l-1)/(c,l-1)/(c-1,l)/(c,l), cujos
        // CENTROS ficam em (índice+0.5). Sem esse -0.5, a amostra bilinear cairia
        // exatamente sobre UM pixel só (fx=fy=0) em vez de misturar os 4 ao redor da quina.
        var (r, g, b) = AmostragemBilinear.Amostrar(imagem, ponto.X - 0.5, ponto.Y - 0.5);

        // Cor de fora: sonda o vizinho REAL, um pouco além da borda ao longo da normal — não
        // uma aproximação global. Se a normal for degenerada (contorno de 1 ponto), cai pra 0.
        if (normal.X == 0 && normal.Y == 0) return 0;

        var pontoDeFora = new PontoXY(ponto.X + normal.X * DistanciaDeSonda, ponto.Y + normal.Y * DistanciaDeSonda);
        var (rf, gf, bf) = AmostragemBilinear.Amostrar(imagem, pontoDeFora.X - 0.5, pontoDeFora.Y - 0.5);

        var vx = rf - corDeDentro.R;
        var vy = gf - corDeDentro.G;
        var vz = bf - corDeDentro.B;
        var comprimentoAoQuadrado = vx * vx + vy * vy + vz * vz;

        if (comprimentoAoQuadrado < 1e-9)
            return 0; // dentro e fora têm a mesma cor — não dá pra inferir subpixel daqui

        var wx = r - corDeDentro.R;
        var wy = g - corDeDentro.G;
        var wz = b - corDeDentro.B;

        var t = (wx * vx + wy * vy + wz * vz) / comprimentoAoQuadrado;
        t = Math.Clamp(t, 0, 1);

        // t=0 (cor amostrada bate com "dentro") -> a borda real está mais pra fora do que
        // o vértice traçado -> desloca +0.5 na normal; t=1 -> desloca -0.5.
        return 0.5 - t;
    }
}
