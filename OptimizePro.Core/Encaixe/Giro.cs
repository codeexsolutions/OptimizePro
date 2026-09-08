namespace OptimizePro.Core.Encaixe;

/// <summary>Porte de <c>ROTACOES_POR_GIRO</c> (§11.11).</summary>
public enum TipoDeGiro
{
    /// <summary>Padrão — mantém o sentido do fio do tecido.</summary>
    MantemSentido,

    /// <summary>Nunca gira (ex.: estampa com "para cima" fixo).</summary>
    Fixa,

    /// <summary>Malha lisa, sem sentido — pode girar em qualquer múltiplo de 90°.</summary>
    Livre,
}

public static class Giro
{
    public static IReadOnlyList<int> RotacoesPara(TipoDeGiro tipo) => tipo switch
    {
        TipoDeGiro.MantemSentido => [0, 180],
        TipoDeGiro.Fixa => [0],
        TipoDeGiro.Livre => [0, 90, 180, 270],
        _ => throw new ArgumentOutOfRangeException(nameof(tipo)),
    };
}
