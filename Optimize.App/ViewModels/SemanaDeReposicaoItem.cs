using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using OptimizePro.Services.Impressoras.Historico;

namespace Optimize.App.ViewModels;

/// <summary>Um trabalho de reposição dentro de uma semana (§22.7) — já formatado pra tela.</summary>
public sealed class ItemDeReposicaoItem(ItemDeReposicao item)
{
    public string Id { get; } = item.Id;
    public string DataHora { get; } = $"{FormatoImpressoras.DataBr(item.Data)} {item.Hora}".Trim();
    public string? NomeDaMaquina { get; } = item.NomeDaMaquina;
    public string? Tarefa { get; } = item.Tarefa;
    public string Metragem { get; } = FormatoImpressoras.Metros(item.ComprimentoDeImpressao);
}

/// <summary>Uma semana de reposição (§22.7) — <see cref="Aberta"/> controla se os trabalhos aparecem, igual ao acordeão de <c>Reposicao.tsx</c>.</summary>
public sealed partial class SemanaDeReposicaoItem : ObservableObject
{
    public string InicioDaSemana { get; }
    public string PeriodoFormatado { get; }
    public string Metragem { get; }
    public int Quantidade { get; }
    public List<ItemDeReposicaoItem> Itens { get; }

    [ObservableProperty]
    public partial bool Aberta { get; set; }

    public SemanaDeReposicaoItem(SemanaDeReposicao semana)
    {
        InicioDaSemana = semana.InicioDaSemana;
        PeriodoFormatado = $"{FormatoImpressoras.DataBr(semana.InicioDaSemana)} a {FormatoImpressoras.DataBr(semana.FimDaSemana)}";
        Metragem = FormatoImpressoras.Metros(semana.MetragemTotal);
        Quantidade = semana.Quantidade;
        Itens = semana.Itens.Select(i => new ItemDeReposicaoItem(i)).ToList();
    }
}
