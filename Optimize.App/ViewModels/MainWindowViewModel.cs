using System;
using System.Collections.Generic;
using System.Globalization;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Optimize.App.Services;

namespace Optimize.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly CultureInfo CulturaRelogio = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IFabricaDeViewModels _fabricaDeViewModels;
    private readonly Timer _timerDoRelogio;

    [ObservableProperty]
    public partial ViewModelBase TelaAtual { get; set; }

    /// <summary>Qual entrada de <see cref="InfoDasTelas"/> alimenta o cabeçalho — separado de <see cref="TelaAtual"/> (o ViewModel em si) porque o cabeçalho só precisa de título/apoio/ícone, não do VM inteiro.</summary>
    [ObservableProperty]
    public partial TipoDeTela TelaSelecionada { get; set; } = TipoDeTela.Moldes;

    public InfoDeTela InfoDaTelaAtual => InfoDasTelas.Todas[TelaSelecionada];

    partial void OnTelaSelecionadaChanged(TipoDeTela value) => OnPropertyChanged(nameof(InfoDaTelaAtual));

    // TODO: refletir o status real assim que IWhatsAppSidecarClient existir (§14).
    [ObservableProperty]
    public partial string StatusWhatsApp { get; set; } = "desconectado";

    /// <summary>Porte de <c>useRelogio.ts</c> — data/hora do cabeçalho, atualizadas de meio em meio minuto (o relógio só mostra hora:minuto, não há o que ganhar olhando mais vezes).</summary>
    [ObservableProperty]
    public partial string RelogioData { get; set; } = "";

    [ObservableProperty]
    public partial string RelogioHora { get; set; } = "";

    public IReadOnlyList<ItemDeMenu> ItensDeMenu { get; } =
    [
        new(TipoDeTela.Moldes, "Moldes", "Produção"),
        new(TipoDeTela.Projetos, "Projetos", "Produção"),
        new(TipoDeTela.Encaixe, "Encaixe", "Produção"),
        new(TipoDeTela.Vetor, "Vetor", "Produção"),
        new(TipoDeTela.Disparo, "Disparo", "Comunicação"),
        new(TipoDeTela.Configuracoes, "Configurações", "Comunicação"),
    ];

    public MainWindowViewModel(IFabricaDeViewModels fabricaDeViewModels, INavegador navegador)
    {
        _fabricaDeViewModels = fabricaDeViewModels;
        TelaAtual = fabricaDeViewModels.Criar(TipoDeTela.Moldes);
        navegador.Navegado += (tipo, parametro) =>
        {
            TelaAtual = _fabricaDeViewModels.Criar(tipo, parametro);
            TelaSelecionada = tipo;
        };

        AtualizarRelogio();
        _timerDoRelogio = new Timer(TimeSpan.FromSeconds(30)) { AutoReset = true };
        // Elapsed dispara numa thread do pool — as propriedades observáveis (bind na UI)
        // só podem ser tocadas na UI thread.
        _timerDoRelogio.Elapsed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(AtualizarRelogio);
        _timerDoRelogio.Start();
    }

    private void AtualizarRelogio()
    {
        var agora = DateTime.Now;
        RelogioData = agora.ToString("ddd, dd 'de' MMM", CulturaRelogio).Replace(".", "");
        RelogioHora = agora.ToString("HH:mm", CulturaRelogio);
    }

    [RelayCommand]
    private void NavegarPara(ItemDeMenu item)
    {
        TelaAtual = _fabricaDeViewModels.Criar(item.Tipo);
        TelaSelecionada = item.Tipo;
    }

    [RelayCommand]
    private void NavegarParaTipo(TipoDeTela tipo)
    {
        TelaAtual = _fabricaDeViewModels.Criar(tipo);
        TelaSelecionada = tipo;
    }
}
