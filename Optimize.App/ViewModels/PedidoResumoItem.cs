using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OptimizePro.Data.Repositorios;

namespace Optimize.App.ViewModels;

/// <summary>Uma linha da lista de Pedidos (§22.8) — o resumo (contadores), sem os itens ainda (carregados só ao expandir).</summary>
public sealed partial class PedidoResumoItem : ObservableObject
{
    public string Id { get; }
    public string CriadoEmFormatado { get; }
    public string Status { get; private set; }
    public string StatusRotulo => RotuloDoStatus(Status);
    public string? Observacao { get; }
    public int QuantidadeDeItens { get; }
    public int QuantidadeOk { get; }
    public int QuantidadeComErro { get; }
    public double FracaoConcluida => QuantidadeDeItens > 0 ? (double)(QuantidadeOk + QuantidadeComErro) / QuantidadeDeItens : 0;

    [ObservableProperty]
    public partial bool Aberto { get; set; }

    [ObservableProperty]
    public partial bool CarregandoDetalhe { get; set; }

    [ObservableProperty]
    public partial bool ConfirmandoExclusao { get; set; }

    public ObservableCollection<PedidoItemItem> Itens { get; } = [];

    public PedidoResumoItem(ResumoDoPedido resumo)
    {
        Id = resumo.Id;
        CriadoEmFormatado = resumo.CriadoEm.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        Status = resumo.Status;
        Observacao = resumo.Observacao;
        QuantidadeDeItens = resumo.QuantidadeDeItens;
        QuantidadeOk = resumo.QuantidadeOk;
        QuantidadeComErro = resumo.QuantidadeComErro;
    }

    public void AtualizarStatus(string status)
    {
        Status = status;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusRotulo));
    }

    public static string RotuloDoStatus(string status) => status switch
    {
        "aberto" => "Em aberto",
        "pausado" => "Pausado",
        "concluido" => "Concluído",
        _ => status,
    };
}
