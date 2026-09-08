namespace OptimizePro.Core.Vetor;

/// <summary>Tangente de entrada/saída de um vértice — iguais quando o vértice é suave, diferentes quando é canto vivo.</summary>
public readonly record struct TangenteDoVertice(PontoXY Entrada, PontoXY Saida);

/// <summary>
/// Porte de <c>tangentesDoContorno</c> (§14.1 passo 7) — usadas depois pra montar a Bézier
/// da remontagem (§14.6). Num vértice suave, a tangente é a direção <c>anterior→próximo</c>
/// (pula o vértice atual de propósito — "não aponta pro vizinho seguinte", é isso que evita
/// a curva ondular). Num canto vivo, a tangente de entrada segue o lado que chega e a de
/// saída segue o lado que sai, sem suavizar — preserva o corte.
/// </summary>
public static class TangentesDoContorno
{
    public static IReadOnlyList<TangenteDoVertice> Calcular(IReadOnlyList<PontoXY> contorno, IReadOnlyList<bool> quinas)
    {
        var n = contorno.Count;
        var resultado = new List<TangenteDoVertice>(n);

        for (var i = 0; i < n; i++)
        {
            var anterior = contorno[(i - 1 + n) % n];
            var atual = contorno[i];
            var proximo = contorno[(i + 1) % n];

            if (quinas[i])
            {
                var tangenteEntrada = Normalizar(new PontoXY(atual.X - anterior.X, atual.Y - anterior.Y));
                var tangenteSaida = Normalizar(new PontoXY(proximo.X - atual.X, proximo.Y - atual.Y));
                resultado.Add(new TangenteDoVertice(tangenteEntrada, tangenteSaida));
            }
            else
            {
                var tangente = Normalizar(new PontoXY(proximo.X - anterior.X, proximo.Y - anterior.Y));
                resultado.Add(new TangenteDoVertice(tangente, tangente));
            }
        }

        return resultado;
    }

    private static PontoXY Normalizar(PontoXY v)
    {
        var comprimento = Math.Sqrt(v.X * v.X + v.Y * v.Y);
        return comprimento < 1e-9 ? new PontoXY(0, 0) : new PontoXY(v.X / comprimento, v.Y / comprimento);
    }
}
