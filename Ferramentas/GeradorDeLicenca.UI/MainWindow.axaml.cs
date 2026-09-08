using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using OptimizePro.Licenciamento;

namespace GeradorDeLicenca.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void GerarChave_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LimparErro();

        var topLevel = TopLevel.GetTopLevel(this)!;
        var arquivo = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Onde salvar a chave PRIVADA (guarde em local seguro, fora do repositório)",
            SuggestedFileName = "chave-privada.pem",
            FileTypeChoices = [new FilePickerFileType("Chave PEM") { Patterns = ["*.pem"] }],
        });
        if (arquivo is null) return;

        try
        {
            using var chave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            await using (var stream = await arquivo.OpenWriteAsync())
            await using (var writer = new StreamWriter(stream))
                await writer.WriteAsync(chave.ExportECPrivateKeyPem());

            var publicaBase64 = Convert.ToBase64String(chave.ExportSubjectPublicKeyInfo());
            ChavePublicaBox.Text = publicaBase64;
            ChavePublicaBox.IsVisible = true;
            CopiarChavePublicaBtn.IsVisible = true;
            CaminhoDaChaveBox.Text = arquivo.TryGetLocalPath() ?? arquivo.Name;
        }
        catch (Exception ex)
        {
            MostrarErro($"Não foi possível gerar/salvar a chave: {ex.Message}");
        }
    }

    private async void SelecionarChaveExistente_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this)!;
        var arquivos = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar chave privada (.pem)",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Chave PEM") { Patterns = ["*.pem"] }],
        });
        if (arquivos.Count > 0)
            CaminhoDaChaveBox.Text = arquivos[0].TryGetLocalPath() ?? arquivos[0].Name;
    }

    private void AtalhoTeste7Dias_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        DiasUpDown.Value = 7;
        TipoCombo.SelectedIndex = 1; // Teste
    }

    private void AtalhoMensal30Dias_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        DiasUpDown.Value = 30;
        TipoCombo.SelectedIndex = 0; // Pago
    }

    private void GerarCodigo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LimparErro();

        var caminhoDaChave = CaminhoDaChaveBox.Text?.Trim();
        var cliente = ClienteBox.Text?.Trim();
        var dias = (int)(DiasUpDown.Value ?? 0);
        var tipo = TipoCombo.SelectedIndex == 1 ? TipoDeLicenca.Teste : TipoDeLicenca.Paga;

        if (string.IsNullOrWhiteSpace(caminhoDaChave) || !File.Exists(caminhoDaChave))
        {
            MostrarErro("Selecione uma chave privada válida (.pem) em '1. Chave de assinatura' antes de gerar um código.");
            return;
        }

        if (string.IsNullOrWhiteSpace(cliente))
        {
            MostrarErro("Informe o cliente (nome ou e-mail) — é só pra rastreabilidade, não trava o código.");
            return;
        }

        if (dias <= 0)
        {
            MostrarErro("A validade em dias precisa ser maior que zero.");
            return;
        }

        try
        {
            using var chave = ECDsa.Create();
            chave.ImportFromPem(File.ReadAllText(caminhoDaChave));

            var validoAte = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(dias);
            var clienteIdHash = CodificadorDeLicenca.HashDoCliente(cliente);
            var codigo = CodificadorDeLicenca.Gerar(chave, validoAte, clienteIdHash, tipo);

            ResultadoBox.Text =
                $"Cliente:    {cliente}\n" +
                $"Tipo:       {(tipo == TipoDeLicenca.Teste ? "Teste" : "Pago")}\n" +
                $"Válido até: {validoAte:dd/MM/yyyy} ({dias} dia(s) a partir de hoje)\n\n" +
                $"{codigo}";
            ResultadoBox.IsVisible = true;
            CopiarCodigoBtn.IsVisible = true;
        }
        catch (Exception ex)
        {
            MostrarErro($"Não foi possível gerar o código — confira se o arquivo é mesmo uma chave privada ECDSA válida. Detalhe: {ex.Message}");
        }
    }

    private async void CopiarChavePublica_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await CopiarParaAreaDeTransferencia(ChavePublicaBox.Text);

    private async void CopiarCodigo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Copia só a última linha (o código em si), não o resumo inteiro acima dele.
        var texto = ResultadoBox.Text ?? "";
        var codigo = texto.Split('\n').LastOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? texto;
        await CopiarParaAreaDeTransferencia(codigo.Trim());
    }

    private async Task CopiarParaAreaDeTransferencia(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(texto);
    }

    private void MostrarErro(string mensagem)
    {
        MensagemDeErro.Text = mensagem;
        MensagemDeErroBorder.IsVisible = true;
    }

    private void LimparErro()
    {
        MensagemDeErroBorder.IsVisible = false;
    }
}
