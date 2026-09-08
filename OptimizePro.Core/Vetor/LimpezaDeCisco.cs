namespace OptimizePro.Core.Vetor;

/// <summary>
/// Porte de <c>limparCisco</c> (§14.1 passo 2) — manchas de cor com menos de
/// <paramref name="tamanhoMinimo"/> pixels (parâmetro "detalhe") são reatribuídas à cor
/// vizinha dominante (mais votos entre os pixels de fronteira da mancha).
/// </summary>
/// <remarks>
/// Passada única: identifica todas as manchas pequenas com base nos índices originais e
/// reatribui todas de uma vez (não reprocessa manchas que "cresceram" por causa de outra
/// reatribuição nesta mesma chamada) — mais simples e determinístico; chamar de novo se
/// precisar de convergência completa.
/// </remarks>
public static class LimpezaDeCisco
{
    private static readonly (int Dx, int Dy)[] Vizinhos4 = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static IReadOnlyList<int> Limpar(IReadOnlyList<int> indicesPorPixel, int largura, int altura, int tamanhoMinimo)
    {
        if (tamanhoMinimo <= 1)
            return indicesPorPixel;

        var visitado = new bool[indicesPorPixel.Count];
        var resultado = indicesPorPixel.ToArray();

        for (var i = 0; i < indicesPorPixel.Count; i++)
        {
            if (visitado[i] || indicesPorPixel[i] < 0)
                continue;

            var componente = ColetarComponente(indicesPorPixel, largura, altura, i, visitado);
            if (componente.Count >= tamanhoMinimo)
                continue;

            var dominante = CorVizinhaDominante(indicesPorPixel, largura, altura, componente);
            if (dominante is not { } d)
                continue;

            foreach (var p in componente)
                resultado[p] = d;
        }

        return resultado;
    }

    private static List<int> ColetarComponente(IReadOnlyList<int> indices, int largura, int altura, int inicio, bool[] visitado)
    {
        var indiceAlvo = indices[inicio];
        var pilha = new Stack<int>();
        pilha.Push(inicio);
        visitado[inicio] = true;

        var componente = new List<int>();

        while (pilha.Count > 0)
        {
            var atual = pilha.Pop();
            componente.Add(atual);

            var x = atual % largura;
            var y = atual / largura;

            foreach (var (dx, dy) in Vizinhos4)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || nx >= largura || ny < 0 || ny >= altura) continue;

                var vizinho = ny * largura + nx;
                if (visitado[vizinho] || indices[vizinho] != indiceAlvo) continue;

                visitado[vizinho] = true;
                pilha.Push(vizinho);
            }
        }

        return componente;
    }

    private static int? CorVizinhaDominante(IReadOnlyList<int> indices, int largura, int altura, List<int> componente)
    {
        var indiceAlvo = indices[componente[0]];
        var votos = new Dictionary<int, int>();

        foreach (var p in componente)
        {
            var x = p % largura;
            var y = p / largura;

            foreach (var (dx, dy) in Vizinhos4)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || nx >= largura || ny < 0 || ny >= altura) continue;

                var idxVizinho = indices[ny * largura + nx];
                if (idxVizinho < 0 || idxVizinho == indiceAlvo) continue;

                votos[idxVizinho] = votos.GetValueOrDefault(idxVizinho) + 1;
            }
        }

        return votos.Count == 0 ? null : votos.OrderByDescending(kv => kv.Value).First().Key;
    }
}
