using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Optimize.App.ViewModels;

namespace Optimize.App.Views;

public partial class MoldeWizardView : UserControl
{
    public MoldeWizardView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Abre o seletor de arquivo nativo e repassa os bytes lidos pra ViewModel — fica aqui
    /// (não na ViewModel) porque depende do <see cref="TopLevel"/>/<see cref="IStorageProvider"/>,
    /// que são conceitos de UI (§8.3: upload por vaga).
    /// </summary>
    private async void EscolherArquivo_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PecaEditavel peca }) return;
        if (DataContext is not MoldeWizardViewModel viewModel) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var arquivos = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar arquivo do molde",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Moldes (DXF, PLT, SVG, PDF)") { Patterns = ["*.dxf", "*.plt", "*.svg", "*.pdf"] },
            ],
        });

        var arquivo = arquivos.FirstOrDefault();
        if (arquivo is null) return;

        await using var stream = await arquivo.OpenReadAsync();
        using var memoria = new MemoryStream();
        await stream.CopyToAsync(memoria);

        await viewModel.CarregarArquivoAsync(peca, arquivo.Name, memoria.ToArray());
    }
}
