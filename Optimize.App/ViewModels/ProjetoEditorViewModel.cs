using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Optimize.App.Services;
using OptimizePro.Services.Projetos;

namespace Optimize.App.ViewModels;

public partial class ProjetoEditorViewModel : ViewModelBase, IRecebeParametro
{
    private readonly IProjetoService _projetoService;
    private readonly INavegador _navegador;
    private int _projetoId;

    [ObservableProperty]
    public partial string Nome { get; set; } = "";

    [ObservableProperty]
    public partial string? Observacoes { get; set; }

    [ObservableProperty]
    public partial double? LarguraTecido { get; set; }

    [ObservableProperty]
    public partial double? Espaco { get; set; }

    [ObservableProperty]
    public partial double? Margem { get; set; }

    [ObservableProperty]
    public partial string? Giro { get; set; }

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial bool Salvando { get; set; }

    [ObservableProperty]
    public partial string? MensagemDeErro { get; set; }

    public ObservableCollection<ProjetoPecaEditavel> Pecas { get; } = [];

    public ProjetoEditorViewModel(IProjetoService projetoService, INavegador navegador)
    {
        _projetoService = projetoService;
        _navegador = navegador;
    }

    public void Receber(object? parametro)
    {
        if (parametro is not int projetoId) return;

        _projetoId = projetoId;
        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        Carregando = true;
        MensagemDeErro = null;
        try
        {
            var projeto = await _projetoService.ObterProjetoAsync(_projetoId);

            Nome = projeto.Nome;
            Observacoes = projeto.Observacoes;
            LarguraTecido = projeto.LarguraTecido;
            Espaco = projeto.Espaco;
            Margem = projeto.Margem;
            Giro = projeto.Giro;

            Pecas.Clear();
            foreach (var peca in projeto.Pecas)
            {
                Pecas.Add(new ProjetoPecaEditavel
                {
                    Nome = peca.Nome,
                    Arquivo = peca.Arquivo,
                    Largura = peca.Largura,
                    Altura = peca.Altura,
                    Quantidade = peca.Quantidade,
                });
            }
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível carregar o projeto: {ex.Message}";
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>Chamado pelo code-behind da View depois do usuário escolher uma imagem (§4.3) — igual ao molde, a leitura do arquivo fica na View por depender do <c>TopLevel</c>/<c>StorageProvider</c>.</summary>
    public async Task CarregarImagemAsync(ProjetoPecaEditavel peca, string nomeDoArquivo, byte[] bytes)
    {
        try
        {
            var arquivo = await _projetoService.SalvarImagemDeProjetoAsync(bytes);
            peca.Arquivo = arquivo;
            peca.NomeDoArquivoOriginal = nomeDoArquivo;
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Erro ao enviar '{nomeDoArquivo}': {ex.Message}";
        }
    }

    [RelayCommand]
    private void AdicionarPeca() => Pecas.Add(new ProjetoPecaEditavel());

    [RelayCommand]
    private void RemoverPeca(ProjetoPecaEditavel peca) => Pecas.Remove(peca);

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (string.IsNullOrWhiteSpace(Nome))
        {
            MensagemDeErro = "Informe o nome do projeto.";
            return;
        }

        var entradas = Pecas
            .Where(p => p.TemArquivo)
            .Select(p => new ProjetoPecaEntrada(p.Nome, p.Arquivo!, p.Largura, p.Altura, p.Quantidade, 0, null))
            .ToList();

        Salvando = true;
        MensagemDeErro = null;
        try
        {
            await _projetoService.AtualizarProjetoAsync(_projetoId, new ProjetoEntrada(Nome, Observacoes, LarguraTecido, Espaco, Margem, Giro, entradas));
            _navegador.NavegarPara(TipoDeTela.Projetos);
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível salvar o projeto: {ex.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }

    [RelayCommand]
    private void Cancelar() => _navegador.NavegarPara(TipoDeTela.Projetos);
}
