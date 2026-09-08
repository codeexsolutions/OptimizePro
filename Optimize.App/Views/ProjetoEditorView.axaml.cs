using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Optimize.App.ViewModels;

namespace Optimize.App.Views;

public partial class ProjetoEditorView : UserControl
{
    public ProjetoEditorView()
    {
        InitializeComponent();
    }

    private async void EscolherImagem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ProjetoPecaEditavel peca }) return;
        if (DataContext is not ProjetoEditorViewModel viewModel) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var arquivos = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar imagem da peça",
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.ImageAll],
        });

        var arquivo = arquivos.FirstOrDefault();
        if (arquivo is null) return;

        await using var stream = await arquivo.OpenReadAsync();
        using var memoria = new MemoryStream();
        await stream.CopyToAsync(memoria);

        await viewModel.CarregarImagemAsync(peca, arquivo.Name, memoria.ToArray());
    }
}
