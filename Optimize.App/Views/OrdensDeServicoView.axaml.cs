using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Optimize.App.ViewModels;

namespace Optimize.App.Views;

public partial class OrdensDeServicoView : UserControl
{
    public OrdensDeServicoView()
    {
        InitializeComponent();
        var botao = this.FindControl<Button>("BotaoEscolherImagens");
        if (botao is not null) botao.Click += EscolherImagens_OnClick;
    }

    private async void EscolherImagens_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OrdensDeServicoViewModel viewModel) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var arquivos = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar imagens de referência",
            AllowMultiple = true,
            FileTypeFilter = [FilePickerFileTypes.ImageAll],
        });
        if (arquivos.Count == 0) return;

        var imagens = new List<ImagemEscolhidaParaOrdem>();
        foreach (var arquivo in arquivos)
        {
            await using var stream = await arquivo.OpenReadAsync();
            using var memoria = new MemoryStream();
            await stream.CopyToAsync(memoria);

            var tipoMime = arquivo.Name.ToLowerInvariant() switch
            {
                var n when n.EndsWith(".png") => "image/png",
                var n when n.EndsWith(".webp") => "image/webp",
                var n when n.EndsWith(".gif") => "image/gif",
                _ => "image/jpeg",
            };
            imagens.Add(new ImagemEscolhidaParaOrdem(arquivo.Name, tipoMime, memoria.ToArray()));
        }

        viewModel.AdicionarImagens(imagens);
    }
}
