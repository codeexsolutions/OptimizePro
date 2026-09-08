namespace OptimizePro.Core.Encaixe;

/// <summary>
/// Porte de "engorde pela folga" (§11.2) — dilatação por distância de Manhattan (diamante,
/// não quadrado): uma célula fica cheia se existir uma célula originalmente cheia a
/// distância Manhattan ≤ raio.
/// </summary>
/// <remarks>
/// Implementado como <paramref name="raio"/> iterações de "dilata 1 célula em cruz (4
/// vizinhos)" — matematicamente idêntico à dilatação Manhattan de raio r num só passo
/// (soma de Minkowski de r cópias do diamante unitário = diamante de raio r), só que O(r)
/// passadas em vez da passada horizontal+vertical separável descrita na especificação.
/// Mais simples de auferir corretude; trocar pela versão separável só se raios grandes
/// (folgas grandes) se mostrarem lentos na prática.
/// </remarks>
public static class Dilatacao
{
    public static bool[,] DilatarManhattan(bool[,] origem, int raio)
    {
        if (raio <= 0)
            return origem;

        var cols = origem.GetLength(0);
        var linhas = origem.GetLength(1);
        var atual = origem;

        for (var passo = 0; passo < raio; passo++)
        {
            var proximo = new bool[cols, linhas];

            for (var c = 0; c < cols; c++)
            {
                for (var l = 0; l < linhas; l++)
                {
                    proximo[c, l] =
                        atual[c, l] ||
                        (c > 0 && atual[c - 1, l]) ||
                        (c < cols - 1 && atual[c + 1, l]) ||
                        (l > 0 && atual[c, l - 1]) ||
                        (l < linhas - 1 && atual[c, l + 1]);
                }
            }

            atual = proximo;
        }

        return atual;
    }
}
