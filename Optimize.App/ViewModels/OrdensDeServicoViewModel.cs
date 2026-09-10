using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OptimizePro.Services.Impressoras;

namespace Optimize.App.ViewModels;

/// <summary>Uma imagem escolhida no formulário de nova OS, ainda não enviada (§22.9) — só o nome aparece na tela, sem preview (fora de escopo por ora).</summary>
public sealed record ImagemEscolhidaParaOrdem(string NomeDoArquivo, string TipoMime, byte[] Dados);

/// <summary>
/// Tela Ordens de Serviço (§22.9) — decisão do usuário: tela completa, com criar E excluir
/// ordem, diferente da própria referência (que reduziu a tela a só o modelo de dados). É o que
/// dá a imagem de referência pro pedido/calandra (via <c>PedidoItem.OrdemId</c>, já modelado
/// desde a Fase 1) e fecha o prefixo "O" do scan deixado pendente no passo 5.
/// </summary>
public partial class OrdensDeServicoViewModel : ViewModelBase
{
    private readonly IOrdemDeServicoService _service;

    public ObservableCollection<OrdemDeServicoItem> Ordens { get; } = [];
    public ObservableCollection<ImagemEscolhidaParaOrdem> ImagensEscolhidas { get; } = [];

    public bool TemOrdens => Ordens.Count > 0;
    public bool TemImagensEscolhidas => ImagensEscolhidas.Count > 0;

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? MensagemDeErro { get; set; }

    [ObservableProperty]
    public partial string Busca { get; set; } = "";

    [ObservableProperty]
    public partial bool MostrandoFormulario { get; set; }

    [ObservableProperty]
    public partial bool Salvando { get; set; }

    // Campos do formulário de nova OS.
    [ObservableProperty]
    public partial string NomeDoCliente { get; set; } = "";

    [ObservableProperty]
    public partial string Tecido { get; set; } = "";

    [ObservableProperty]
    public partial string TamanhoDeImpressao { get; set; } = "";

    [ObservableProperty]
    public partial string Metros { get; set; } = "";

    [ObservableProperty]
    public partial string Operador { get; set; } = "";

    [ObservableProperty]
    public partial string Maquina { get; set; } = "";

    [ObservableProperty]
    public partial DateTimeOffset Data { get; set; } = DateTimeOffset.Now;

    [ObservableProperty]
    public partial string Observacao { get; set; } = "";

    public OrdensDeServicoViewModel(IOrdemDeServicoService service)
    {
        _service = service;
        Ordens.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TemOrdens));
        ImagensEscolhidas.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TemImagensEscolhidas));
        _ = CarregarAsync();
    }

    partial void OnBuscaChanged(string value) => _ = CarregarAsync();

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Carregando = true;
        MensagemDeErro = null;
        try
        {
            var resumos = await _service.ListarAsync(string.IsNullOrWhiteSpace(Busca) ? null : Busca);
            Ordens.Clear();
            foreach (var resumo in resumos) Ordens.Add(new OrdemDeServicoItem(resumo));
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível carregar as ordens: {ex.Message}";
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private void AbrirFormulario()
    {
        LimparFormulario();
        MostrandoFormulario = true;
    }

    [RelayCommand]
    private void FecharFormulario()
    {
        MostrandoFormulario = false;
        LimparFormulario();
    }

    private void LimparFormulario()
    {
        NomeDoCliente = "";
        Tecido = "";
        TamanhoDeImpressao = "";
        Metros = "";
        Operador = "";
        Maquina = "";
        Data = DateTimeOffset.Now;
        Observacao = "";
        ImagensEscolhidas.Clear();
    }

    public void AdicionarImagens(IEnumerable<ImagemEscolhidaParaOrdem> imagens)
    {
        foreach (var imagem in imagens) ImagensEscolhidas.Add(imagem);
    }

    [RelayCommand]
    private void RemoverImagemEscolhida(ImagemEscolhidaParaOrdem imagem) => ImagensEscolhidas.Remove(imagem);

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Salvando = true;
        MensagemDeErro = null;
        try
        {
            double? metros = double.TryParse(Metros.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : null;

            await _service.CriarAsync(
                NomeDoCliente, Tecido, TamanhoDeImpressao, metros, Operador, Maquina,
                Data.ToString("yyyy-MM-dd"), Observacao,
                ImagensEscolhidas.Select(i => new ImagemParaOrdemDeServico(i.NomeDoArquivo, i.TipoMime, i.Dados)).ToList());

            MostrandoFormulario = false;
            LimparFormulario();
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível salvar a OS: {ex.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }

    [RelayCommand]
    private void PedirConfirmacaoDeExclusao(OrdemDeServicoItem ordem) => ordem.ConfirmandoExclusao = true;

    [RelayCommand]
    private void CancelarExclusao(OrdemDeServicoItem ordem) => ordem.ConfirmandoExclusao = false;

    [RelayCommand]
    private async Task ExcluirAsync(OrdemDeServicoItem ordem)
    {
        try
        {
            if (await _service.ExcluirAsync(ordem.Id))
                Ordens.Remove(ordem);
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível excluir a OS: {ex.Message}";
        }
    }
}
