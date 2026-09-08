using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Optimize.App.ViewModels;

namespace Optimize.App.Views;

public partial class EncaixeView : UserControl
{
    public EncaixeView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not EncaixeViewModel viewModel) return;
        var itens = e.Data.GetFiles();
        if (itens is null) return;

        var arquivos = itens.OfType<IStorageFile>().Where(f => viewModel.AceitaExtensao(f.Name)).ToList();
        if (arquivos.Count == 0) return;

        await CarregarArquivosAsync(viewModel, arquivos);
    }

    /// <summary>
    /// Abre o seletor de arquivo nativo (múltiplos) e repassa os bytes lidos pra ViewModel —
    /// fica aqui (não na ViewModel) porque depende do <see cref="TopLevel"/>/<see cref="IStorageProvider"/>,
    /// que são conceitos de UI (§8.3).
    /// </summary>
    private async void AdicionarArquivos_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not EncaixeViewModel viewModel) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var arquivos = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar arquivos pra encaixar",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Moldes e artes (DXF, PLT, SVG, PDF, PNG, JPG)") { Patterns = ["*.dxf", "*.plt", "*.svg", "*.pdf", "*.png", "*.jpg", "*.jpeg"] },
            ],
        });

        if (arquivos.Count == 0) return;
        await CarregarArquivosAsync(viewModel, arquivos);
    }

    private static async Task CarregarArquivosAsync(EncaixeViewModel viewModel, IReadOnlyList<IStorageFile> arquivos)
    {
        var lidos = new List<(string Nome, byte[] Bytes)>();

        foreach (var arquivo in arquivos)
        {
            await using var stream = await arquivo.OpenReadAsync();
            using var memoria = new MemoryStream();
            await stream.CopyToAsync(memoria);
            lidos.Add((arquivo.Name, memoria.ToArray()));
        }

        await viewModel.AdicionarArquivosAsync(lidos);
    }
}
