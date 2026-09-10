using CommunityToolkit.Mvvm.ComponentModel;
using OptimizePro.Data.Repositorios;
using OptimizePro.Services.Impressoras.Historico;

namespace Optimize.App.ViewModels;

/// <summary>Uma linha da lista de Ordens de Serviço (§22.9) — já formatada pra tela.</summary>
public sealed partial class OrdemDeServicoItem : ObservableObject
{
    public string Id { get; }
    public string NomeDoCliente { get; }
    public string? Tecido { get; }
    public string DataFormatada { get; }
    public string Metros { get; }
    public int QuantidadeDeImagens { get; }
    public bool TemImagens { get; }

    [ObservableProperty]
    public partial bool ConfirmandoExclusao { get; set; }

    public OrdemDeServicoItem(ResumoDeOrdemDeServico resumo)
    {
        Id = resumo.Id;
        NomeDoCliente = resumo.NomeDoCliente;
        Tecido = resumo.Tecido;
        DataFormatada = FormatoImpressoras.DataBr(resumo.Data);
        Metros = resumo.Metros is { } m ? FormatoImpressoras.Metros(m) : "—";
        QuantidadeDeImagens = resumo.QuantidadeDeImagens;
        TemImagens = resumo.QuantidadeDeImagens > 0;
    }
}
