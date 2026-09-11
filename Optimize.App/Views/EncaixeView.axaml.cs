using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
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

    private async void SalvarPdf_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not EncaixeViewModel viewModel) return;

        var bytes = viewModel.GerarPdf();
        if (bytes is null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var arquivo = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar encaixe em PDF (tamanho real)",
            SuggestedFileName = "encaixe.pdf",
            FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }],
        });
        if (arquivo is null) return;

        await using var stream = await arquivo.OpenWriteAsync();
        await stream.WriteAsync(bytes);
    }

    /// <summary>
    /// "Baixar" do menu Exportar do projeto full: lá é um PNG de prévia (não o arquivo pra
    /// corte — isso é o PDF), tirado do próprio canvas na tela a 4px/cm. Aqui o equivalente é
    /// rasterizar a mesma árvore visual que já está na tela (<see cref="TecidoPreviewGrid"/>),
    /// só que numa resolução maior que a da tela, pra sair legível.
    /// </summary>
    private async void Baixar_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not EncaixeViewModel { Resultado: { } resultado } viewModel) return;
        if (TecidoPreviewGrid.Bounds is not { Width: > 0, Height: > 0 } bounds) return;

        const double fatorDeQualidade = 4; // mesmo espírito do "4 px por cm" do original: legível pra levar pra mesa de corte.
        var pixelSize = new PixelSize((int)Math.Ceiling(bounds.Width * fatorDeQualidade), (int)Math.Ceiling(bounds.Height * fatorDeQualidade));
        using var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96 * fatorDeQualidade, 96 * fatorDeQualidade));
        bitmap.Render(TecidoPreviewGrid);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        // Mesmo nome de arquivo do original (a vírgula é pra bater com o nome que o PDF já usava).
        var nome = $"encaixe-{(resultado.ConsumoCm / 100).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace(".", ",")}m.png";
        var arquivo = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Baixar prévia do encaixe (PNG)",
            SuggestedFileName = nome,
            FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }],
        });
        if (arquivo is null) return;

        await using var stream = await arquivo.OpenWriteAsync();
        bitmap.Save(stream);
    }

    /// <summary>
    /// "Imprimir" do projeto full é <c>window.print()</c> — não existe no desktop. O
    /// equivalente aqui é gerar o mesmo PDF em tamanho real e mandar pra impressora padrão via
    /// o verbo de shell "print" (o mesmo mecanismo que "Imprimir" no menu de contexto do
    /// Explorer usa), sem abrir nenhum visualizador na tela.
    /// </summary>
    private void Imprimir_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not EncaixeViewModel viewModel) return;

        var bytes = viewModel.GerarPdf();
        if (bytes is null) return;

        var caminho = Path.Combine(Path.GetTempPath(), $"encaixe-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(caminho, bytes);

        try
        {
            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true, Verb = "print" });
        }
        catch
        {
            // Sem verbo "print" registrado (ex.: sem leitor de PDF padrão instalado) — abre o
            // arquivo mesmo assim, pra imprimir na mão em vez de falhar em silêncio.
            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
        }
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
