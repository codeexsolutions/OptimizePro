using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Optimize.App.Services;

namespace Optimize.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFabricaDeViewModels _fabricaDeViewModels;

    [ObservableProperty]
    public partial ViewModelBase TelaAtual { get; set; }

    // TODO: refletir o status real assim que IWhatsAppSidecarClient existir (§14).
    [ObservableProperty]
    public partial string StatusWhatsApp { get; set; } = "desconectado";

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
        navegador.Navegado += (tipo, parametro) => TelaAtual = _fabricaDeViewModels.Criar(tipo, parametro);
    }

    [RelayCommand]
    private void NavegarPara(ItemDeMenu item) => TelaAtual = _fabricaDeViewModels.Criar(item.Tipo);

    [RelayCommand]
    private void NavegarParaTipo(TipoDeTela tipo) => TelaAtual = _fabricaDeViewModels.Criar(tipo);
}
