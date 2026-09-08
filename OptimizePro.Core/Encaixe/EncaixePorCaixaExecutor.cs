namespace OptimizePro.Core.Encaixe;

public sealed record PosicaoDeItemCaixa(string ItemId, double X, double Y, double Largura, double Altura, bool Deitada);

public sealed record ResultadoCaixa(IReadOnlyList<PosicaoDeItemCaixa> Posicoes, IReadOnlyList<string> ItensNaoEncaixados, double FundoMaximoCm, double AreaRealCm2);

/// <summary>
/// Laço externo do encaixador por caixa (§11.4): assenta cada item na ordem dada. Quando
/// <c>PermiteDeitar</c> é verdadeiro (agrupamento "deitada" — só existe se a peça tiver
/// giro "livre"), compara a pontuação da orientação normal com a orientação deitada
/// (largura↔altura trocadas) e usa a que pontuar melhor.
/// </summary>
public static class EncaixePorCaixaExecutor
{
    public static ResultadoCaixa Encaixar(
        double larguraTecidoCm, IReadOnlyList<(ItemParaCaixa Item, bool PermiteDeitar)> ordem, HeuristicaDeCaixa heuristica)
    {
        var encaixador = new EncaixadorPorCaixa(larguraTecidoCm);
        var posicoes = new List<PosicaoDeItemCaixa>();
        var naoEncaixados = new List<string>();
        var fundoMaximo = 0.0;
        var areaReal = 0.0;

        foreach (var (item, permiteDeitar) in ordem)
        {
            var normal = encaixador.EncontrarMelhorPosicao(item.LarguraCm, item.AlturaCm, heuristica);

            var ehQuadrado = Math.Abs(item.LarguraCm - item.AlturaCm) < 1e-9;
            var deitado = permiteDeitar && !ehQuadrado
                ? encaixador.EncontrarMelhorPosicao(item.AlturaCm, item.LarguraCm, heuristica)
                : null;

            var usarDeitado = deitado is { } d && (normal is not { } n || d.P1 < n.P1 || (d.P1 == n.P1 && d.P2 < n.P2));
            var escolhida = usarDeitado ? deitado : normal;

            if (escolhida is not { } e)
            {
                naoEncaixados.Add(item.Id);
                continue;
            }

            encaixador.Confirmar(e);
            posicoes.Add(new PosicaoDeItemCaixa(item.Id, e.X, e.Y, e.Largura, e.Altura, usarDeitado));

            areaReal += item.AreaRealCm2;

            var topoDoItem = e.Y + e.Altura;
            if (topoDoItem > fundoMaximo) fundoMaximo = topoDoItem;
        }

        return new ResultadoCaixa(posicoes, naoEncaixados, fundoMaximo, areaReal);
    }
}
