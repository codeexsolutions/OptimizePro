namespace OptimizePro.Core.Encaixe;

/// <summary>
/// Porte de "engorde pela folga" (§11.2) — dilatação por DISCO (distância euclidiana),
/// não mais Manhattan.
/// </summary>
/// <remarks>
/// Corrigido (02/09/2026, achado comparando com <c>engordar()</c> em
/// <c>public/encaixe-mascara.js</c> do projeto de referência, que por sua vez já tinha sido
/// corrigido lá de um erro pior: um engorde horizontal+vertical SEPARÁVEL desenhava um
/// QUADRADO, chegando a 41% maior que o raio pedido nos cantos em diagonal). O diamante de
/// Manhattan que este arquivo usava antes tem o erro OPOSTO — ele é sempre um SUBCONJUNTO do
/// disco de mesmo raio (distância euclidiana ≤ distância Manhattan sempre), então ele
/// SUBDIMENSIONA a margem em contato diagonal. Para raios pequenos (r≤2) os dois coincidem
/// exatamente (é só a partir de r≥3 que um ponto como (dx,dy)=(2,2) — dentro do disco, fora do
/// diamante — aparece), mas folgas maiores em tecido de folga larga caem direto nessa faixa.
/// Errar a margem pra MENOS é o lado ruim do erro (a peça encosta e estraga o corte); pra mais
/// só gasta um tiquinho de tecido — por isso o disco (que nunca subdimensiona) é a escolha
/// certa, igual à referência.
/// </remarks>
public static class Dilatacao
{
    public static bool[,] Dilatar(bool[,] origem, int raio)
    {
        if (raio <= 0)
            return origem;

        var cols = origem.GetLength(0);
        var linhas = origem.GetLength(1);
        var resultado = new bool[cols, linhas];
        var raioAoQuadrado = raio * raio;

        // Offsets do disco, ordenados do centro pra fora — a maioria das células cheias tem
        // uma origem bem perto, então o caso comum sai rápido com a busca em ordem crescente
        // de distância.
        var offsets = new List<(int Dx, int Dy)>();
        for (var dx = -raio; dx <= raio; dx++)
        {
            for (var dy = -raio; dy <= raio; dy++)
            {
                if (dx * dx + dy * dy <= raioAoQuadrado)
                    offsets.Add((dx, dy));
            }
        }
        offsets.Sort((a, b) => (a.Dx * a.Dx + a.Dy * a.Dy).CompareTo(b.Dx * b.Dx + b.Dy * b.Dy));

        for (var c = 0; c < cols; c++)
        {
            for (var l = 0; l < linhas; l++)
            {
                if (origem[c, l])
                {
                    resultado[c, l] = true;
                    continue;
                }

                foreach (var (dx, dy) in offsets)
                {
                    var nc = c + dx;
                    var nl = l + dy;
                    if (nc < 0 || nc >= cols || nl < 0 || nl >= linhas) continue;
                    if (origem[nc, nl])
                    {
                        resultado[c, l] = true;
                        break;
                    }
                }
            }
        }

        return resultado;
    }
}
