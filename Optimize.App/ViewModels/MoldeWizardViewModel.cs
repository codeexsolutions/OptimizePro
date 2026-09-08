using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Optimize.App.Services;
using OptimizePro.Core.Moldes;
using OptimizePro.Services.Moldes;

namespace Optimize.App.ViewModels;

public partial class MoldeWizardViewModel : ViewModelBase
{
    private readonly IMoldeService _moldeService;
    private readonly IReadOnlyList<ILeitorDeMolde> _leitores;
    private readonly INavegador _navegador;

    [ObservableProperty]
    public partial string Nome { get; set; } = "";

    [ObservableProperty]
    public partial string? Observacoes { get; set; }

    [ObservableProperty]
    public partial bool Salvando { get; set; }

    [ObservableProperty]
    public partial string? MensagemDeErro { get; set; }

    public ObservableCollection<PecaEditavel> Pecas { get; } = [];

    public IReadOnlyList<string> Papeis { get; }

    public MoldeWizardViewModel(IMoldeService moldeService, IEnumerable<ILeitorDeMolde> leitores, INavegador navegador)
    {
        _moldeService = moldeService;
        _leitores = [.. leitores];
        _navegador = navegador;
        Papeis = moldeService.ListarPapeis();

        AdicionarPeca();
    }

    [RelayCommand]
    private void AdicionarPeca() => Pecas.Add(new PecaEditavel());

    [RelayCommand]
    private void RemoverPeca(PecaEditavel peca) => Pecas.Remove(peca);

    /// <summary>Chamado pelo code-behind da View depois do usuário escolher um arquivo (§8.3) — a leitura de arquivo em si fica na View porque depende do <c>TopLevel</c>/<c>StorageProvider</c>, que não pertence à ViewModel.</summary>
    public async Task CarregarArquivoAsync(PecaEditavel peca, string nomeDoArquivo, byte[] bytes)
    {
        var extensao = Path.GetExtension(nomeDoArquivo).TrimStart('.');
        var leitor = _leitores.FirstOrDefault(l => l.SuportaExtensao(extensao));

        if (leitor is null)
        {
            MensagemDeErro = $"Formato '.{extensao}' não é suportado (use DXF, PLT, SVG ou PDF).";
            return;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            var resultado = await leitor.LerAsync(stream, new OpcoesLeituraMolde(null, ModoLeituraVetor.Marcador));

            if (resultado.Erro is not null)
            {
                MensagemDeErro = resultado.Erro;
                return;
            }

            var pecaLida = resultado.Pecas.FirstOrDefault();
            if (pecaLida is null)
            {
                MensagemDeErro = $"Nenhuma peça encontrada em '{nomeDoArquivo}'.";
                return;
            }

            peca.DefinirGeometria([.. pecaLida.Contorno], pecaLida.Furos.Count > 0 ? pecaLida.Furos.Select(f => f.ToList()).ToList() : null);
            peca.Largura = Math.Round(pecaLida.LarguraCm, 1);
            peca.Altura = Math.Round(pecaLida.AlturaCm, 1);
            peca.NomeDoArquivo = nomeDoArquivo;
            peca.Origem = $"{extensao.ToUpperInvariant()} · {resultado.Unidade}";
            if (string.IsNullOrWhiteSpace(peca.Nome))
                peca.Nome = Path.GetFileNameWithoutExtension(nomeDoArquivo);

            MensagemDeErro = null;
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Erro ao ler '{nomeDoArquivo}': {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (string.IsNullOrWhiteSpace(Nome))
        {
            MensagemDeErro = "Informe o nome do molde.";
            return;
        }

        var entradas = Pecas
            .Where(p => p.TemContorno)
            .Select(p => new PecaEntrada(p.Tamanho, p.Papel, p.Nome, p.Quantidade, p.Largura, p.Altura, p.Contorno, p.Furos, p.Origem))
            .ToList();

        if (entradas.Count == 0)
        {
            MensagemDeErro = "Adicione ao menos uma peça com um arquivo válido antes de salvar.";
            return;
        }

        Salvando = true;
        MensagemDeErro = null;
        try
        {
            await _moldeService.CriarAsync(new MoldeEntrada(Nome, Observacoes, entradas));
            _navegador.NavegarPara(TipoDeTela.Moldes);
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível salvar o molde: {ex.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }

    [RelayCommand]
    private void Cancelar() => _navegador.NavegarPara(TipoDeTela.Moldes);
}
