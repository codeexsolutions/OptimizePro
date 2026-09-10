using OptimizePro.Core.Impressoras;
using OptimizePro.Data.Entidades;
using OptimizePro.Services.Impressoras;

namespace Optimize.App.ViewModels;

/// <summary>Projeção de <see cref="Maquina"/> pra grade da tela (§22.3) — rótulo do tipo pronto, sem lógica na view.</summary>
public sealed class MaquinaItem(Maquina maquina)
{
    public string Id { get; } = maquina.Id;
    public string Nome { get; } = maquina.Nome;
    public string RotuloDoTipo { get; } = VarreduraDeRedeService.RotuloDoTipo(maquina.Tipo);
    public bool Habilitada { get; } = maquina.Habilitada;
    public string? Host { get; } = maquina.Host;
    public string? Ip { get; } = maquina.Ip;
    public string Origem { get; } = maquina.Origem;
}

/// <summary>Uma máquina achada na varredura, ainda sem nome — vira <see cref="Maquina"/> só depois do cadastro.</summary>
public sealed partial class MaquinaPendenteItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public required string Host { get; init; }
    public string? Ip { get; init; }
    public required TipoDeMaquina Tipo { get; init; }
    public required string RotuloDoTipo { get; init; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string NomeSugerido { get; set; } = "";
}
