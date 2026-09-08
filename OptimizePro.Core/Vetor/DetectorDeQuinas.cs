namespace OptimizePro.Core.Vetor;

/// <summary>
/// Porte de <c>acharQuinas</c> (§14.1 passo 7): um vértice é "canto vivo" quando o ângulo
/// entre a direção de chegada e a de saída fica abaixo do parâmetro "quina" (padrão 55°).
/// </summary>
/// <remarks>
/// O ângulo medido é entre os vetores <c>anterior-atual</c> e <c>próximo-atual</c> (ambos
/// saindo do vértice): numa reta perfeita esses vetores apontam em direções opostas
/// (180°); numa reversão bem fechada (pico), quase na mesma direção (perto de 0°) — por
/// isso "abaixo do limiar = canto vivo" (§14.1: tabela de parâmetros).
/// </remarks>
public static class DetectorDeQuinas
{
    public static IReadOnlyList<bool> AcharQuinas(IReadOnlyList<PontoXY> contorno, double quinaGraus)
    {
        var n = contorno.Count;
        var resultado = new bool[n];

        for (var i = 0; i < n; i++)
        {
            var anterior = contorno[(i - 1 + n) % n];
            var atual = contorno[i];
            var proximo = contorno[(i + 1) % n];

            var angulo = AnguloEntreVetores(anterior.X - atual.X, anterior.Y - atual.Y, proximo.X - atual.X, proximo.Y - atual.Y);
            resultado[i] = angulo < quinaGraus;
        }

        return resultado;
    }

    private static double AnguloEntreVetores(double ax, double ay, double bx, double by)
    {
        var moduloA = Math.Sqrt(ax * ax + ay * ay);
        var moduloB = Math.Sqrt(bx * bx + by * by);

        if (moduloA < 1e-9 || moduloB < 1e-9)
            return 180; // aresta degenerada (comprimento zero) — trata como reto, não é quina

        var cosseno = Math.Clamp((ax * bx + ay * by) / (moduloA * moduloB), -1, 1);
        return Math.Acos(cosseno) * 180.0 / Math.PI;
    }
}
