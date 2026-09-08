using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using OptimizePro.Core;

namespace Optimize.App.ViewModels;

/// <summary>Linha editável do assistente de criação de molde — envoltório mutável sobre os dados que viram <c>PecaEntrada</c> ao salvar (§8.3).</summary>
public partial class PecaEditavel : ObservableObject
{
    [ObservableProperty]
    public partial string Papel { get; set; } = "outro";

    [ObservableProperty]
    public partial string? Nome { get; set; }

    [ObservableProperty]
    public partial string Tamanho { get; set; } = "único";

    [ObservableProperty]
    public partial int Quantidade { get; set; } = 1;

    [ObservableProperty]
    public partial double Largura { get; set; }

    [ObservableProperty]
    public partial double Altura { get; set; }

    [ObservableProperty]
    public partial string? NomeDoArquivo { get; set; }

    [ObservableProperty]
    public partial string? Origem { get; set; }

    public List<PontoXY> Contorno { get; private set; } = [];
    public List<List<PontoXY>>? Furos { get; private set; }

    public bool TemContorno => Contorno.Count >= 3;

    public void DefinirGeometria(List<PontoXY> contorno, List<List<PontoXY>>? furos)
    {
        Contorno = contorno;
        Furos = furos;
        OnPropertyChanged(nameof(TemContorno));
    }
}
