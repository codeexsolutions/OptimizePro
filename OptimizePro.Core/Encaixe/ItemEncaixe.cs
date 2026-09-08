namespace OptimizePro.Core.Encaixe;

/// <summary>Uma peça a encaixar — já expandida em unidade (§11.1: "peças expandem em itens; 1 cópia cada").</summary>
public sealed record ItemEncaixe(string Id, IReadOnlyDictionary<int, Mascara> MascarasPorRotacao)
{
    /// <summary>Constrói as 2/4 máscaras (conforme <paramref name="rotacoesPermitidas"/>) a partir da máscara na rotação 0.</summary>
    public static ItemEncaixe DeMascaraBase(string id, Mascara mascaraRotacao0, IReadOnlyList<int> rotacoesPermitidas)
    {
        var mascaras = rotacoesPermitidas.ToDictionary(r => r, r => RotacaoDeMascara.Rotacionar(mascaraRotacao0, r));
        return new ItemEncaixe(id, mascaras);
    }
}
