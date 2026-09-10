using CommunityToolkit.Mvvm.ComponentModel;
using OptimizePro.Data.Entidades;
using OptimizePro.Services.Impressoras.Historico;

namespace Optimize.App.ViewModels;

/// <summary>Projeção de <see cref="RegistroDeImpressao"/> pra tabela de Histórico (§22.5/§22.8) — já formatado, nada de lógica na view. <see cref="Marcado"/> é a seleção pra "Lançar pedido".</summary>
public sealed partial class RegistroDeImpressaoItem : ObservableObject
{
    public string Id { get; }
    public string? MaquinaId { get; }
    public string DataHora { get; }
    public string? NomeDaMaquina { get; }
    public string? Tarefa { get; }
    public double ComprimentoBruto { get; }
    public string Metragem { get; }
    public bool MetricaEstimada { get; }
    public string Tempo { get; }
    public string Tinta { get; }
    public bool TintaExperimental { get; }
    public string Situacao { get; }
    public string? Data { get; }

    [ObservableProperty]
    public partial bool Marcado { get; set; }

    public RegistroDeImpressaoItem(RegistroDeImpressao registro)
    {
        Id = registro.Id;
        MaquinaId = registro.MaquinaId;
        DataHora = $"{FormatoImpressoras.DataBr(registro.Data)} {registro.Hora}".Trim();
        NomeDaMaquina = registro.NomeDaMaquina;
        Tarefa = registro.Tarefa;
        ComprimentoBruto = registro.ComprimentoDeImpressao;
        Metragem = FormatoImpressoras.Metros(registro.ComprimentoDeImpressao);
        MetricaEstimada = registro.MetricaEstimada;
        Tempo = FormatoImpressoras.Duracao(registro.TempoSegundos);
        Tinta = FormatoImpressoras.Tinta(registro.TintaMl);
        TintaExperimental = registro.TintaExperimental && registro.TintaMl > 0;
        Situacao = registro.Cancelada ? "Cancelado" : registro.ComErro ? "Erro" : registro.EstadoDoProgresso == "printing" ? registro.Status ?? "Em impressão" : registro.Status ?? "Concluído";
        Data = registro.Data;
    }
}
