namespace OptimizePro.Core.Vetor;

public readonly record struct EntradaDeHistograma(double R, double G, double B, long Peso);

/// <summary>
/// Porte de <c>juntarCores</c> (§14.2): quantiza para RGB555 antes de agrupar (reduz o
/// universo de cores distintas antes do median cut).
/// </summary>
/// <remarks>
/// <b>Preferência por pixels de "miolo" (02/09/2026 — porte da peneira que faltava):</b> um
/// pixel de BORDA (algum vizinho 4-conectado com cor bem diferente, ou vizinho transparente)
/// tem sua cor real "contaminada" pelo anti-aliasing — é uma mistura das duas regiões, não a
/// cor de nenhuma das duas de verdade. Contar pixel de borda igual a pixel de miolo distorce o
/// median cut: com poucas cores pedidas, uma faixa inteira de tons de mistura de borda pode
/// "roubar" um balde de cor que deveria ir pra uma região real (a causa raiz de logos com
/// degradê saindo com cor vazando entre regiões sem relação, achado testando com uma logo
/// real). Por isso o histograma agora conta preferencialmente só pixels de MIOLO (longe de
/// transição de cor); só cai pra "conta tudo" se o miolo for menos de 5% do total — imagens
/// muito finas (linhas de 1px) podem não ter miolo suficiente, e nesse caso é melhor uma
/// paleta imperfeita do que nenhuma.
/// </remarks>
public static class HistogramaDeCores
{
    private const double LimiarDeBorda = 40;
    private const double FracaoMinimaDeMiolo = 0.05;

    public static IReadOnlyList<EntradaDeHistograma> Construir(ImagemRgba imagem)
    {
        var contagensTudo = new Dictionary<int, (long Peso, long SomaR, long SomaG, long SomaB)>();
        var contagensMiolo = new Dictionary<int, (long Peso, long SomaR, long SomaG, long SomaB)>();
        long totalOpacos = 0;
        long totalMiolo = 0;

        for (var y = 0; y < imagem.Altura; y++)
        {
            for (var x = 0; x < imagem.Largura; x++)
            {
                var (r, g, b, a) = imagem.ObterPixel(x, y);
                if (a == 0)
                    continue; // transparente não é cor nenhuma

                totalOpacos++;
                var chave = QuantizarRgb555(r, g, b);
                Acumular(contagensTudo, chave, r, g, b);

                if (!EhPixelDeBorda(imagem, x, y, r, g, b))
                {
                    totalMiolo++;
                    Acumular(contagensMiolo, chave, r, g, b);
                }
            }
        }

        var usarMiolo = totalOpacos > 0 && (double)totalMiolo / totalOpacos >= FracaoMinimaDeMiolo;
        var fonte = usarMiolo ? contagensMiolo : contagensTudo;

        return [.. fonte.Values.Select(v => new EntradaDeHistograma((double)v.SomaR / v.Peso, (double)v.SomaG / v.Peso, (double)v.SomaB / v.Peso, v.Peso))];
    }

    private static void Acumular(Dictionary<int, (long Peso, long SomaR, long SomaG, long SomaB)> contagens, int chave, byte r, byte g, byte b)
    {
        contagens.TryGetValue(chave, out var atual);
        contagens[chave] = (atual.Peso + 1, atual.SomaR + r, atual.SomaG + g, atual.SomaB + b);
    }

    private static readonly (int Dx, int Dy)[] Vizinhos4 = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>Pixel de borda: tem algum vizinho 4-conectado transparente ou com cor bem diferente (mistura de anti-aliasing, não representa nenhuma das duas regiões de verdade).</summary>
    private static bool EhPixelDeBorda(ImagemRgba imagem, int x, int y, byte r, byte g, byte b)
    {
        foreach (var (dx, dy) in Vizinhos4)
        {
            var nx = x + dx;
            var ny = y + dy;
            if (nx < 0 || nx >= imagem.Largura || ny < 0 || ny >= imagem.Altura)
                continue;

            var (nr, ng, nb, na) = imagem.ObterPixel(nx, ny);
            if (na == 0)
                return true;

            var distancia = Math.Sqrt(Math.Pow(r - nr, 2) + Math.Pow(g - ng, 2) + Math.Pow(b - nb, 2));
            if (distancia > LimiarDeBorda)
                return true;
        }

        return false;
    }

    private static int QuantizarRgb555(byte r, byte g, byte b) => ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
}
