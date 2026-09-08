namespace OptimizePro.Core.Contorno;

public sealed record ContornoExtraido(IReadOnlyList<PontoXY> Pontos, bool Furo);

/// <summary>
/// Extrai contorno(s) vetorial(is) de uma grade booleana — coleta as arestas de fronteira
/// célula a célula e encadeia em laços fechados, separando contorno externo de furo pelo
/// sinal da área. Vértices em coordenadas de célula da grade (não cm/px). Compartilhado
/// entre o Encaixe (§11.5 "pecaEmPoligonos" passo 2, NFP) e o Vetor (§14.1 "contornosDoMapa",
/// uma chamada por cor da paleta).
/// </summary>
/// <remarks>
/// Implementado como coleta de arestas + encadeamento, sem os "16 estados de vizinhança"
/// completos do marching squares das especificações originais — não resolve corretamente
/// o caso raro de duas regiões se tocando só na diagonal de uma célula (ambiguidade de
/// "sela" nas quinas). Improvável em silhuetas/manchas de cor reais nessa resolução;
/// documentado como limitação conhecida em vez de implementar os 16 estados completos,
/// que só valem a pena se isso se mostrar um problema de verdade.
/// </remarks>
public static class ExtracaoDeContorno
{
    public static IReadOnlyList<ContornoExtraido> Extrair(int colunas, int linhas, Func<int, int, bool> cheio)
    {
        bool CheioComLimite(int c, int l) => c >= 0 && c < colunas && l >= 0 && l < linhas && cheio(c, l);

        var arestas = new Dictionary<(int X, int Y), (int X, int Y)>();

        for (var c = 0; c < colunas; c++)
        {
            for (var l = 0; l < linhas; l++)
            {
                if (!CheioComLimite(c, l)) continue;

                if (!CheioComLimite(c, l - 1)) arestas[(c, l)] = (c + 1, l);
                if (!CheioComLimite(c + 1, l)) arestas[(c + 1, l)] = (c + 1, l + 1);
                if (!CheioComLimite(c, l + 1)) arestas[(c + 1, l + 1)] = (c, l + 1);
                if (!CheioComLimite(c - 1, l)) arestas[(c, l + 1)] = (c, l);
            }
        }

        var resultado = new List<ContornoExtraido>();
        var visitados = new HashSet<(int, int)>();

        foreach (var inicio in arestas.Keys)
        {
            if (visitados.Contains(inicio))
                continue;

            var pontos = new List<PontoXY>();
            var atual = inicio;

            do
            {
                visitados.Add(atual);
                pontos.Add(new PontoXY(atual.Item1, atual.Item2));
                atual = arestas[atual];
            } while (atual != inicio && !visitados.Contains(atual));

            if (pontos.Count < 3)
                continue;

            pontos.Add(pontos[0]);
            var simplificado = Geometria.Simplificar(pontos, tolerancia: 0.01);

            var area = Geometria.AreaComSinal(simplificado);
            if (Math.Abs(area) < 1e-9)
                continue;

            resultado.Add(new ContornoExtraido(simplificado, Furo: area < 0));
        }

        return resultado;
    }
}
