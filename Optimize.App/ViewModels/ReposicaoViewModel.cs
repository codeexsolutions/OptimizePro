using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using Optimize.App.Services;
using OptimizePro.Services.Impressoras.Historico;

namespace Optimize.App.ViewModels;

/// <summary>
/// Tela Reposição (§22.7) — porte de <c>Reposicao.tsx</c>: trabalho refeito, por semana.
/// Reconhecido pela palavra "reposição" no nome do arquivo (sem acento/caixa) — não é um campo
/// do sistema, é uma convenção da produção. Por isso o número é um piso, não um total.
/// </summary>
public partial class ReposicaoViewModel : ViewModelBase
{
    private readonly IReposicaoService _reposicaoService;
    private readonly ClientePainelEmTempoReal _cliente;

    public ObservableCollection<SemanaDeReposicaoItem> Semanas { get; } = [];

    public bool TemDados => QuantidadeTotal > 0;
    public bool MostrarVazio => !Carregando && !TemDados;

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? MensagemDeErro { get; set; }

    [ObservableProperty]
    public partial string MetragemTotal { get; set; } = "0,00 m";

    [ObservableProperty]
    public partial int QuantidadeTotal { get; set; }

    [ObservableProperty]
    public partial string Resumo { get; set; } = "";

    public ReposicaoViewModel(IReposicaoService reposicaoService, ClientePainelEmTempoReal cliente)
    {
        _reposicaoService = reposicaoService;
        _cliente = cliente;

        _cliente.EventoRecebido += OnEventoRecebido;
        _ = _cliente.GarantirConectadoAsync();
        _ = CarregarAsync();
    }

    private void OnEventoRecebido(string evento)
    {
        if (evento is "new-print" or "history-updated")
            Dispatcher.UIThread.Post(() => _ = CarregarAsync());
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Carregando = true;
        MensagemDeErro = null;
        try
        {
            var resposta = await _reposicaoService.ObterAsync();

            Semanas.Clear();
            foreach (var semana in resposta.Semanas) Semanas.Add(new SemanaDeReposicaoItem(semana));
            if (Semanas.Count > 0) Semanas[0].Aberta = true; // a mais recente já vem aberta, igual à referência.

            MetragemTotal = FormatoImpressoras.MetrosCurtos(resposta.MetragemTotal);
            QuantidadeTotal = resposta.QuantidadeTotal;

            var media = resposta.Semanas.Count > 0 ? resposta.MetragemTotal / resposta.Semanas.Count : 0;
            Resumo = $"{resposta.QuantidadeTotal} reposição(ões) em {resposta.Semanas.Count} semana(s) — média de {FormatoImpressoras.MetrosCurtos(media)} por semana.";
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível carregar a reposição: {ex.Message}";
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private void Alternar(SemanaDeReposicaoItem semana) => semana.Aberta = !semana.Aberta;

    partial void OnCarregandoChanged(bool value) => OnPropertyChanged(nameof(MostrarVazio));
    partial void OnQuantidadeTotalChanged(int value)
    {
        OnPropertyChanged(nameof(TemDados));
        OnPropertyChanged(nameof(MostrarVazio));
    }
}
