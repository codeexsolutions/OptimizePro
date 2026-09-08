using OptimizePro.Core.Contorno;

namespace OptimizePro.Core.Encaixe.Nfp;

/// <summary>Extrai o contorno vetorial de uma <see cref="Mascara"/> (§11.5 "pecaEmPoligonos" passo 2) via <see cref="ExtracaoDeContorno"/>.</summary>
public static class ExtratorDeContorno
{
    public static IReadOnlyList<ContornoExtraido> ExtrairContornos(Mascara mascara) =>
        ExtracaoDeContorno.Extrair(mascara.Colunas, mascara.Linhas, (c, l) => mascara.ObterCheio(c, l) == 1);
}
