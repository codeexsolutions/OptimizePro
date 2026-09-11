using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OptimizePro.Services.Vetor;

namespace Optimize.App.ViewModels;

/// <summary>
/// Parâmetros de vetorização + as duas prévias (§9.4). Toda mudança de parâmetro reexecuta
/// <see cref="IVetorService.VetorizarAsync"/> com um debounce leve (§9.4) — não a cada tecla,
/// só depois de ~300ms sem mudança. A prévia do SVG gerado é desenhada com
/// <c>Avalonia.Controls.Shapes.Path</c> direto (a sintaxe de path do Avalonia já é
/// compatível com M/L/C/A/Z do SVG), sem precisar de biblioteca de SVG externa.
/// </summary>
public partial class VetorViewModel : ViewModelBase
{
    private static readonly Regex RegexDeCamada = new("""<path d="([^"]+)" fill="(#[0-9A-Fa-f]{6})" fill-rule="evenodd"/>""", RegexOptions.Compiled);

    private readonly IVetorService _vetorService;
    private byte[]? _bytesOriginais;
    private CancellationTokenSource? _debounceCts;
    private ResultadoDeVetorizacao? _ultimoResultado;

    [ObservableProperty]
    public partial string? NomeDoArquivo { get; set; }

    [ObservableProperty]
    public partial Bitmap? ImagemOriginal { get; set; }

    public ObservableCollection<CamadaPreview> Camadas { get; } = [];

    [ObservableProperty]
    public partial double LarguraPx { get; set; }

    [ObservableProperty]
    public partial double AlturaPx { get; set; }

    [ObservableProperty]
    public partial bool Processando { get; set; }

    [ObservableProperty]
    public partial string? MensagemDeErro { get; set; }

    [ObservableProperty]
    public partial int Cores { get; set; } = 6;

    [ObservableProperty]
    public partial int Detalhe { get; set; } = 8;

    [ObservableProperty]
    public partial double Suavidade { get; set; } = 1;

    [ObservableProperty]
    public partial double QuinaGraus { get; set; } = 55;

    [ObservableProperty]
    public partial double Tensao { get; set; } = 1;

    [ObservableProperty]
    public partial bool Redondas { get; set; } = true;

    [ObservableProperty]
    public partial bool Subpixel { get; set; } = true;

    [ObservableProperty]
    public partial double JuntarSombras { get; set; }

    /// <summary>
    /// "Plano B" (02/09/2026) — usa o Potrace de verdade via processo externo em vez do
    /// traçado próprio, pra arte complexa (degradê, várias cores) onde o motor próprio ainda
    /// distorce forma. Precisa do executável VetorGpl publicado ao lado do app (não vem
    /// embutido por licença GPL) — se não achar, a mensagem de erro explica isso.
    /// Marcado por padrão (10/09/2026, decisão do usuário antes de empacotar pro cliente) —
    /// o empacotamento precisa publicar Ferramentas/VetorGpl junto do instalador pra isto
    /// funcionar de cara, sem exigir que o usuário mexa em configuração alguma.
    /// </summary>
    [ObservableProperty]
    public partial bool UsarMotorExterno { get; set; } = true;

    /// <summary>
    /// Sugestão automática de "Cores" pra imagem carregada (null antes de calcular). Só um
    /// chute inicial — o campo "Cores" continua 100% editável; existe pra evitar o usuário
    /// escolher um preset com poucas cores demais pra uma imagem rica em cor (degradê etc.) e
    /// levar vazamento de cor entre regiões sem perceber o motivo.
    /// </summary>
    [ObservableProperty]
    public partial int? SugestaoDeCores { get; set; }

    public bool TemImagem => ImagemOriginal is not null;

    [ObservableProperty]
    public partial bool TemResultado { get; set; }

    public VetorViewModel(IVetorService vetorService)
    {
        _vetorService = vetorService;
    }

    partial void OnCoresChanged(int value) => AgendarVetorizacao();
    partial void OnDetalheChanged(int value) => AgendarVetorizacao();
    partial void OnSuavidadeChanged(double value) => AgendarVetorizacao();
    partial void OnQuinaGrausChanged(double value) => AgendarVetorizacao();
    partial void OnTensaoChanged(double value) => AgendarVetorizacao();
    partial void OnRedondasChanged(bool value) => AgendarVetorizacao();
    partial void OnSubpixelChanged(bool value) => AgendarVetorizacao();
    partial void OnJuntarSombrasChanged(double value) => AgendarVetorizacao();
    partial void OnUsarMotorExternoChanged(bool value) => AgendarVetorizacao();

    /// <summary>Chamado pelo code-behind da View depois do usuário escolher um arquivo (mesmo padrão de Moldes/Projetos — leitura de arquivo fica na View, que depende do <c>TopLevel</c>/<c>StorageProvider</c>).</summary>
    public void CarregarImagem(string nomeDoArquivo, byte[] bytes)
    {
        _bytesOriginais = bytes;
        NomeDoArquivo = nomeDoArquivo;
        MensagemDeErro = null;
        SugestaoDeCores = null;
        _ultimoResultado = null;
        TemResultado = false;

        try
        {
            ImagemOriginal = new Bitmap(new MemoryStream(bytes));
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível abrir a imagem: {ex.Message}";
            ImagemOriginal = null;
        }

        OnPropertyChanged(nameof(TemImagem));
        AgendarVetorizacao();

        if (ImagemOriginal is not null)
            _ = AplicarSugestaoDeCoresAsync(bytes);
    }

    /// <summary>
    /// Roda em paralelo ao primeiro encaixe (não bloqueia) — assim que a sugestão sai, aplica
    /// direto em "Cores" (o campo continua editável depois; é só um chute inicial melhor que
    /// um número fixo de preset). Descartada se o usuário já trocou de imagem nesse meio-tempo.
    /// </summary>
    private async Task AplicarSugestaoDeCoresAsync(byte[] bytesDestaImagem)
    {
        try
        {
            var sugestao = await _vetorService.SugerirNumeroDeCoresAsync(bytesDestaImagem);
            if (!ReferenceEquals(_bytesOriginais, bytesDestaImagem)) return; // imagem trocou enquanto calculava

            SugestaoDeCores = sugestao;
            Cores = sugestao;
        }
        catch (Exception)
        {
            // Sugestão é só um acelerador de UX — falhar aqui não pode impedir o usuário de
            // vetorizar com o valor de "Cores" que já estava (padrão ou de um preset).
        }
    }

    /// <summary>
    /// "Salvar como PDF" (02/09/2026) — o código-behind da View chama isto depois de já ter
    /// perguntado ao usuário onde salvar (depende de <c>TopLevel</c>/<c>StorageProvider</c>,
    /// mesmo padrão de <see cref="CarregarImagem"/>). Devolve null se não há resultado ainda.
    /// </summary>
    public byte[]? GerarPdf() => _ultimoResultado is { } r
        ? ExportadorDeVetorParaPdf.Converter(r.Svg, r.LarguraPx, r.AlturaPx)
        : null;

    [RelayCommand]
    private void AplicarChapada() => AplicarOpcoes(AtalhosDeVetorizacao.Chapada);

    [RelayCommand]
    private void AplicarSilhueta() => AplicarOpcoes(AtalhosDeVetorizacao.Silhueta);

    [RelayCommand]
    private void AplicarSombra() => AplicarOpcoes(AtalhosDeVetorizacao.Sombra);

    [RelayCommand]
    private void AplicarFino() => AplicarOpcoes(AtalhosDeVetorizacao.Fino);

    private void AplicarOpcoes(OpcoesDeVetorizacao opcoes)
    {
        Cores = opcoes.Cores;
        Detalhe = opcoes.Detalhe;
        Suavidade = opcoes.Suavidade;
        QuinaGraus = opcoes.QuinaGraus;
        Tensao = opcoes.Tensao;
        Redondas = opcoes.Redondas;
        Subpixel = opcoes.Subpixel;
        JuntarSombras = opcoes.JuntarSombras;
        AgendarVetorizacao();
    }

    private void AgendarVetorizacao()
    {
        if (_bytesOriginais is null) return;

        _debounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        _ = DebounceEVetorizarAsync(cts.Token);
    }

    private async Task DebounceEVetorizarAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(300, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested || _bytesOriginais is null) return;

        Processando = true;
        MensagemDeErro = null;
        try
        {
            var opcoes = new OpcoesDeVetorizacao(Cores, Detalhe, Suavidade, QuinaGraus, Tensao, Redondas, Subpixel, JuntarSombras, UsarMotorExterno);
            var resultado = await _vetorService.VetorizarAsync(_bytesOriginais, opcoes, ct);

            if (ct.IsCancellationRequested) return;

            LarguraPx = resultado.LarguraPx;
            AlturaPx = resultado.AlturaPx;
            _ultimoResultado = resultado;
            TemResultado = true;

            Camadas.Clear();
            foreach (Match m in RegexDeCamada.Matches(resultado.Svg))
            {
                var geometria = Geometry.Parse(m.Groups[1].Value);
                Camadas.Add(new CamadaPreview(geometria, new SolidColorBrush(Color.Parse(m.Groups[2].Value))));
            }
        }
        catch (OperationCanceledException)
        {
            // Debounce cancelado por uma mudança de parâmetro mais recente — normal.
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível vetorizar: {ex.Message}";
            _ultimoResultado = null;
            TemResultado = false;
        }
        finally
        {
            if (!ct.IsCancellationRequested) Processando = false;
        }
    }
}
