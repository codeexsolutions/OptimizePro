using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OptimizePro.Services.Configuracoes;

namespace Optimize.App.ViewModels;

public partial class ConfiguracoesViewModel : ViewModelBase
{
    private readonly IConfiguracaoService _configuracaoService;

    [ObservableProperty]
    public partial string CodigoPaisPadrao { get; set; } = "";

    [ObservableProperty]
    public partial string DddPadrao { get; set; } = "";

    [ObservableProperty]
    public partial string MensagemPadraoDeDisparo { get; set; } = "";

    [ObservableProperty]
    public partial int DelayMinimoMs { get; set; }

    [ObservableProperty]
    public partial int DelayMaximoMs { get; set; }

    [ObservableProperty]
    public partial int DpiPadraoDeExportacao { get; set; }

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial bool Salvando { get; set; }

    [ObservableProperty]
    public partial string? MensagemDeErro { get; set; }

    [ObservableProperty]
    public partial string? MensagemDeSucesso { get; set; }

    public ConfiguracoesViewModel(IConfiguracaoService configuracaoService)
    {
        _configuracaoService = configuracaoService;
        _ = CarregarAsync();
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Carregando = true;
        MensagemDeErro = null;
        try
        {
            var configuracoes = await _configuracaoService.ObterAsync();
            CodigoPaisPadrao = configuracoes.CodigoPaisPadrao;
            DddPadrao = configuracoes.DddPadrao;
            MensagemPadraoDeDisparo = configuracoes.MensagemPadraoDeDisparo;
            DelayMinimoMs = configuracoes.DelayMinimoMs;
            DelayMaximoMs = configuracoes.DelayMaximoMs;
            DpiPadraoDeExportacao = configuracoes.DpiPadraoDeExportacao;
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível carregar as configurações: {ex.Message}";
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Salvando = true;
        MensagemDeErro = null;
        MensagemDeSucesso = null;
        try
        {
            var configuracoes = new ConfiguracoesDoApp(
                CodigoPaisPadrao, DddPadrao, MensagemPadraoDeDisparo, DelayMinimoMs, DelayMaximoMs, DpiPadraoDeExportacao);

            await _configuracaoService.SalvarAsync(configuracoes);
            MensagemDeSucesso = "Configurações salvas.";
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível salvar: {ex.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }
}
