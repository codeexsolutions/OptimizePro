namespace Optimize.App.ViewModels;

/// <summary>Um cartão do painel Impressoras (§22.6) — já pronto pra tela, sem lógica na view.</summary>
public sealed class ImpressoraCardItem
{
    public required string Id { get; init; }
    public required string Nome { get; init; }
    public required string RotuloDoTipo { get; init; }

    /// <summary>null = ainda não se sabe (nenhum evento/leitura de status chegou desde que o app abriu).</summary>
    public bool? Online { get; init; }

    public int TrabalhosHoje { get; init; }
    public string MetragemHoje { get; init; } = "0,00 m";
    public string? UltimoTrabalho { get; init; }
    public string? UltimoHorario { get; init; }
}
