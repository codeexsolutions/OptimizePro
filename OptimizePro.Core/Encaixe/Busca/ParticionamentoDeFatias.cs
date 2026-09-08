namespace OptimizePro.Core.Encaixe.Busca;

/// <summary>
/// Porte de §11.9: quantas fatias (workers) rodar e como dividir as receitas entre elas —
/// round-robin, com reserva opcional da última fatia para NFP.
/// </summary>
public static class ParticionamentoDeFatias
{
    public const int MinimoDeFatias = 1;
    public const int MaximoDeFatias = 8;

    /// <summary><c>clamp(hardwareConcurrency-1, 1, 8)</c> — 1 núcleo reservado à UI.</summary>
    public static int NumeroDeFatias(int? processadoresDisponiveis = null) =>
        Math.Clamp((processadoresDisponiveis ?? Environment.ProcessorCount) - 1, MinimoDeFatias, MaximoDeFatias);

    /// <summary>Fatia k recebe receitas de índice k, k+n, k+2n, ... (§11.9).</summary>
    public static IReadOnlyList<IReadOnlyList<Receita>> ParticionarRoundRobin(IReadOnlyList<Receita> receitas, int numeroDeFatias)
    {
        if (numeroDeFatias < 1)
            throw new ArgumentOutOfRangeException(nameof(numeroDeFatias), "Precisa de pelo menos 1 fatia.");

        var fatias = new List<Receita>[numeroDeFatias];
        for (var i = 0; i < numeroDeFatias; i++)
            fatias[i] = [];

        for (var i = 0; i < receitas.Count; i++)
            fatias[i % numeroDeFatias].Add(receitas[i]);

        return fatias;
    }

    /// <summary>
    /// Quando <paramref name="reservarUltimaFatiaParaNfp"/> e há ≥3 fatias, a última fica
    /// só com receitas de motor NFP (mesmo que não haja nenhuma — a reserva é do "modo",
    /// não condicional a ter receita NFP de fato) e as demais dividem o resto em
    /// round-robin (§11.9: "modo automático... com ≥3 workers, a última fatia roda
    /// exclusivamente NFP").
    /// </summary>
    public static (IReadOnlyList<IReadOnlyList<Receita>> FatiasNormais, IReadOnlyList<Receita> FatiaNfp) ParticionarComReservaDeNfp(
        IReadOnlyList<Receita> receitas, int numeroDeFatias, bool reservarUltimaFatiaParaNfp)
    {
        if (!reservarUltimaFatiaParaNfp || numeroDeFatias < 3)
            return (ParticionarRoundRobin(receitas, numeroDeFatias), []);

        var nfp = receitas.Where(r => r.Motor == MotorDeEncaixe.Nfp).ToList();
        var outras = receitas.Where(r => r.Motor != MotorDeEncaixe.Nfp).ToList();

        return (ParticionarRoundRobin(outras, numeroDeFatias - 1), nfp);
    }
}
