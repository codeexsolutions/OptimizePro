using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Optimize.App.ViewModels;

namespace Optimize.App.Views;

public partial class VetorView : UserControl
{
    public VetorView()
    {
        InitializeComponent();
    }

    private async void EscolherImagem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not VetorViewModel viewModel) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var arquivos = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar imagem",
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.ImageAll],
        });

        var arquivo = arquivos.FirstOrDefault();
        if (arquivo is null) return;

        await using var stream = await arquivo.OpenReadAsync();
        using var memoria = new MemoryStream();
        await stream.CopyToAsync(memoria);

        viewModel.CarregarImagem(arquivo.Name, memoria.ToArray());
    }

    private async void SalvarPdf_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not VetorViewModel viewModel) return;

        var bytes = viewModel.GerarPdf();
        if (bytes is null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var nomeSugerido = Path.GetFileNameWithoutExtension(viewModel.NomeDoArquivo ?? "vetor") + ".pdf";
        var arquivo = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar como PDF",
            SuggestedFileName = nomeSugerido,
            FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }],
        });
        if (arquivo is null) return;

        await using var stream = await arquivo.OpenWriteAsync();
        await stream.WriteAsync(bytes);
    }
}
