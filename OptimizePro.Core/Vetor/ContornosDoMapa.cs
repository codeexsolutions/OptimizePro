using OptimizePro.Core.Contorno;

namespace OptimizePro.Core.Vetor;

/// <summary>
/// Porte de <c>contornosDoMapa</c> (§14.1 passo 3) — extrai os contornos fechados
/// (externos e furos) de uma cor da paleta a partir do mapa de índices por pixel.
/// </summary>
public static class ContornosDoMapa
{
    public static IReadOnlyList<ContornoExtraido> Extrair(int largura, int altura, IReadOnlyList<int> indicesPorPixel, int indiceAlvo) =>
        ExtracaoDeContorno.Extrair(largura, altura, (c, l) => indicesPorPixel[l * largura + c] == indiceAlvo);
}
