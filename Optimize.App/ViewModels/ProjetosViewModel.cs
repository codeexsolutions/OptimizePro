using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Optimize.App.Services;
using OptimizePro.Services.Projetos;

namespace Optimize.App.ViewModels;

/// <summary>Navegação em duas colunas: clientes à esquerda, projetos do cliente selecionado à direita (§9.2).</summary>
public partial class ProjetosViewModel : ViewModelBase
{
    private readonly IProjetoService _projetoService;
    private readonly INavegador _navegador;

    public ObservableCollection<ClienteResumo> Clientes { get; } = [];
    public ObservableCollection<ProjetoResumo> ProjetosDoCliente { get; } = [];

    [ObservableProperty]
    public partial ClienteResumo? ClienteSelecionado { get; set; }

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? MensagemDeErro { get; set; }

    [ObservableProperty]
    public partial string NovoClienteNome { get; set; } = "";

    [ObservableProperty]
    public partial string NovoProjetoNome { get; set; } = "";

    public bool TemClientes => Clientes.Count > 0;
    public bool TemClienteSelecionado => ClienteSelecionado is not null;
    public bool TemProjetos => ProjetosDoCliente.Count > 0;
    public bool MostrarClienteSemProjetos => TemClienteSelecionado && !TemProjetos;

    public string TituloProjetos => ClienteSelecionado is { } cliente ? $"Projetos de {cliente.Nome}" : "Selecione um cliente";

    public ProjetosViewModel(IProjetoService projetoService, INavegador navegador)
    {
        _projetoService = projetoService;
        _navegador = navegador;
        _ = CarregarClientesAsync();
    }

    partial void OnClienteSelecionadoChanged(ClienteResumo? value)
    {
        OnPropertyChanged(nameof(TemClienteSelecionado));
        OnPropertyChanged(nameof(MostrarClienteSemProjetos));
        OnPropertyChanged(nameof(TituloProjetos));
        _ = CarregarProjetosDoClienteAsync();
    }

    [RelayCommand]
    private async Task CarregarClientesAsync()
    {
        Carregando = true;
        MensagemDeErro = null;
        try
        {
            var lista = await _projetoService.ListarClientesAsync();
            var idSelecionado = ClienteSelecionado?.Id;

            Clientes.Clear();
            foreach (var cliente in lista) Clientes.Add(cliente);
            OnPropertyChanged(nameof(TemClientes));

            ClienteSelecionado = idSelecionado is { } id ? lista.FirstOrDefault(c => c.Id == id) : null;
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível carregar os clientes: {ex.Message}";
        }
        finally
        {
            Carregando = false;
        }
    }

    private async Task CarregarProjetosDoClienteAsync()
    {
        ProjetosDoCliente.Clear();
        NotificarProjetos();

        if (ClienteSelecionado is not { } cliente) return;

        try
        {
            var comProjetos = await _projetoService.ListarProjetosDoClienteAsync(cliente.Id);
            foreach (var projeto in comProjetos.Projetos) ProjetosDoCliente.Add(projeto);
            NotificarProjetos();
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível carregar os projetos: {ex.Message}";
        }
    }

    private void NotificarProjetos()
    {
        OnPropertyChanged(nameof(TemProjetos));
        OnPropertyChanged(nameof(MostrarClienteSemProjetos));
    }

    [RelayCommand]
    private async Task CriarClienteAsync()
    {
        if (string.IsNullOrWhiteSpace(NovoClienteNome)) return;

        try
        {
            await _projetoService.CriarClienteAsync(new ClienteEntrada(NovoClienteNome, null));
            NovoClienteNome = "";
            await CarregarClientesAsync();
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível criar o cliente: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExcluirClienteAsync(ClienteResumo cliente)
    {
        try
        {
            await _projetoService.ExcluirClienteAsync(cliente.Id);
            if (ClienteSelecionado?.Id == cliente.Id) ClienteSelecionado = null;
            await CarregarClientesAsync();
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível excluir '{cliente.Nome}': {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CriarProjetoAsync()
    {
        if (ClienteSelecionado is not { } cliente || string.IsNullOrWhiteSpace(NovoProjetoNome)) return;

        try
        {
            var id = await _projetoService.CriarProjetoAsync(cliente.Id, NovoProjetoNome);
            NovoProjetoNome = "";
            await CarregarProjetosDoClienteAsync();
            _navegador.NavegarPara(TipoDeTela.ProjetoEditor, id);
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível criar o projeto: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExcluirProjetoAsync(ProjetoResumo projeto)
    {
        try
        {
            await _projetoService.ExcluirProjetoAsync(projeto.Id);
            await CarregarProjetosDoClienteAsync();
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível excluir '{projeto.Nome}': {ex.Message}";
        }
    }

    [RelayCommand]
    private void AbrirProjeto(ProjetoResumo projeto) => _navegador.NavegarPara(TipoDeTela.ProjetoEditor, projeto.Id);
}
