using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Optimize.App.Services;
using OptimizePro.Services.Moldes;

namespace Optimize.App.ViewModels;

public partial class MoldesViewModel : ViewModelBase
{
    private readonly IMoldeService _moldeService;
    private readonly INavegador _navegador;

    public ObservableCollection<MoldeResumo> Moldes { get; } = [];

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? MensagemDeErro { get; set; }

    public bool TemMoldes => Moldes.Count > 0;

    public MoldesViewModel(IMoldeService moldeService, INavegador navegador)
    {
        _moldeService = moldeService;
        _navegador = navegador;
        _ = CarregarAsync();
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Carregando = true;
        MensagemDeErro = null;
        try
        {
            var lista = await _moldeService.ListarAsync();
            Moldes.Clear();
            foreach (var molde in lista) Moldes.Add(molde);
            OnPropertyChanged(nameof(TemMoldes));
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível carregar os moldes: {ex.Message}";
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private void AdicionarMolde() => _navegador.NavegarPara(TipoDeTela.MoldeWizard);

    [RelayCommand]
    private async Task ExcluirMoldeAsync(MoldeResumo molde)
    {
        try
        {
            await _moldeService.ExcluirAsync(molde.Id);
            Moldes.Remove(molde);
            OnPropertyChanged(nameof(TemMoldes));
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível excluir '{molde.Nome}': {ex.Message}";
        }
    }
}
