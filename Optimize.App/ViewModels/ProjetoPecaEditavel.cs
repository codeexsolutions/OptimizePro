using CommunityToolkit.Mvvm.ComponentModel;

namespace Optimize.App.ViewModels;

/// <summary>Linha editável do editor de projeto — envoltório mutável sobre os dados que viram <c>ProjetoPecaEntrada</c> ao salvar (§4.3).</summary>
public partial class ProjetoPecaEditavel : ObservableObject
{
    [ObservableProperty]
    public partial string Nome { get; set; } = "";

    [ObservableProperty]
    public partial string? Arquivo { get; set; }

    [ObservableProperty]
    public partial string? NomeDoArquivoOriginal { get; set; }

    [ObservableProperty]
    public partial double Largura { get; set; }

    [ObservableProperty]
    public partial double Altura { get; set; }

    [ObservableProperty]
    public partial int Quantidade { get; set; } = 1;

    public bool TemArquivo => !string.IsNullOrEmpty(Arquivo);

    partial void OnArquivoChanged(string? value) => OnPropertyChanged(nameof(TemArquivo));
}
