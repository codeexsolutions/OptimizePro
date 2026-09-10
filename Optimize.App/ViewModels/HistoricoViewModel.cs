using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OptimizePro.Core.Impressoras;
using OptimizePro.Data.Entidades;
using OptimizePro.Services.Impressoras;
using OptimizePro.Services.Impressoras.Historico;

namespace Optimize.App.ViewModels;

/// <summary>
/// Tela de Histórico (§22.5) — porte de <c>Historico.tsx</c> no modo Lista (o modo Produção,
/// com preview de imagem, e o lançamento de pedido ficam para quando as telas de Pedidos e o
/// servidor de preview existirem).
/// </summary>
public partial class HistoricoViewModel : ViewModelBase
{
    private readonly IHistoricoService _historicoService;
    private readonly IMaquinaService _maquinaService;
    private readonly IPedidoService _pedidoService;

    public ObservableCollection<MaquinaItem> Maquinas { get; } = [];
    public ObservableCollection<RegistroDeImpressaoItem> Registros { get; } = [];

    public bool TemRegistros => Registros.Count > 0;

    [ObservableProperty]
    public partial DateTimeOffset DataInicio { get; set; } = DateTimeOffset.Now.AddDays(-6);

    [ObservableProperty]
    public partial DateTimeOffset DataFim { get; set; } = DateTimeOffset.Now;

    [ObservableProperty]
    public partial MaquinaItem? MaquinaSelecionada { get; set; }

    [ObservableProperty]
    public partial string Busca { get; set; } = "";

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial bool Atualizando { get; set; }

    [ObservableProperty]
    public partial string? MensagemDeErro { get; set; }

    [ObservableProperty]
    public partial int Trabalhos { get; set; }

    [ObservableProperty]
    public partial int Concluidos { get; set; }

    [ObservableProperty]
    public partial int Cancelados { get; set; }

    [ObservableProperty]
    public partial string Metragem { get; set; } = "0,00 m";

    [ObservableProperty]
    public partial string Tempo { get; set; } = "—";

    [ObservableProperty]
    public partial string Tinta { get; set; } = "—";

    [ObservableProperty]
    public partial int QuantidadeMarcada { get; set; }

    [ObservableProperty]
    public partial string MetragemMarcada { get; set; } = "0,00 m";

    public bool TemSelecao => QuantidadeMarcada > 0;

    [ObservableProperty]
    public partial bool LancandoPedido { get; set; }

    [ObservableProperty]
    public partial bool PedidoCriado { get; set; }

    private List<RegistroDeImpressao> _todosOsRegistros = [];

    public HistoricoViewModel(IHistoricoService historicoService, IMaquinaService maquinaService, IPedidoService pedidoService)
    {
        _historicoService = historicoService;
        _maquinaService = maquinaService;
        _pedidoService = pedidoService;
        Registros.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TemRegistros));
        _ = InicializarAsync();
    }

    private async Task InicializarAsync()
    {
        var maquinas = await _maquinaService.ListarAsync(incluirDesabilitadas: false);
        Maquinas.Clear();
        Maquinas.Add(new MaquinaItem(new Maquina { Id = "all", Nome = "Todas", Tipo = default }));
        foreach (var maquina in maquinas) Maquinas.Add(new MaquinaItem(maquina));
        MaquinaSelecionada = Maquinas[0];

        await CarregarAsync();
    }

    partial void OnDataInicioChanged(DateTimeOffset value) => _ = CarregarAsync();
    partial void OnDataFimChanged(DateTimeOffset value) => _ = CarregarAsync();
    partial void OnMaquinaSelecionadaChanged(MaquinaItem? value) => _ = CarregarAsync();
    partial void OnBuscaChanged(string value) => AplicarFiltroLocal();

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Carregando = true;
        MensagemDeErro = null;
        try
        {
            var inicio = DataInicio.ToString("yyyy-MM-dd");
            var fim = DataFim.ToString("yyyy-MM-dd");
            var resposta = await _historicoService.ObterAsync(MaquinaSelecionada?.Id, inicio, fim);

            _todosOsRegistros = resposta.Registros;
            AplicarFiltroLocal();

            Trabalhos = resposta.Resumo.Trabalhos;
            Concluidos = resposta.Resumo.Concluidos;
            Cancelados = resposta.Resumo.Cancelados;
            Metragem = FormatoImpressoras.MetrosCurtos(resposta.Resumo.MetragemTotal);
            Tempo = FormatoImpressoras.Duracao(resposta.Resumo.TempoSegundosTotal);
            Tinta = FormatoImpressoras.Tinta(resposta.Resumo.TintaMlTotal);
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível carregar o histórico: {ex.Message}";
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private async Task AtualizarAgoraAsync()
    {
        Atualizando = true;
        MensagemDeErro = null;
        try
        {
            var inicio = DataInicio.ToString("yyyy-MM-dd");
            var fim = DataFim.ToString("yyyy-MM-dd");
            await _historicoService.AtualizarAgoraAsync(inicio, fim);
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível atualizar: {ex.Message}";
        }
        finally
        {
            Atualizando = false;
        }
    }

    // A busca é local de propósito (porte da mesma decisão em Historico.tsx): o intervalo já
    // veio inteiro do servidor/banco, filtrar no cliente é instantâneo.
    private void AplicarFiltroLocal()
    {
        var termo = Busca.Trim();
        var filtrados = string.IsNullOrEmpty(termo)
            ? _todosOsRegistros
            : _todosOsRegistros.Where(r => (r.Tarefa ?? "").Contains(termo, StringComparison.OrdinalIgnoreCase)).ToList();

        Registros.Clear();
        foreach (var registro in filtrados)
        {
            var item = new RegistroDeImpressaoItem(registro);
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(RegistroDeImpressaoItem.Marcado)) AtualizarSelecao(); };
            Registros.Add(item);
        }
        AtualizarSelecao();
    }

    private void AtualizarSelecao()
    {
        var marcados = Registros.Where(r => r.Marcado).ToList();
        QuantidadeMarcada = marcados.Count;
        MetragemMarcada = FormatoImpressoras.Metros(marcados.Sum(r => r.ComprimentoBruto));
        OnPropertyChanged(nameof(TemSelecao));
    }

    [RelayCommand]
    private void LimparSelecao()
    {
        foreach (var registro in Registros.Where(r => r.Marcado)) registro.Marcado = false;
    }

    [RelayCommand]
    private async Task LancarPedidoAsync()
    {
        var marcados = Registros.Where(r => r.Marcado).ToList();
        if (marcados.Count == 0) return;

        LancandoPedido = true;
        PedidoCriado = false;
        MensagemDeErro = null;
        try
        {
            var itens = marcados
                .Select(r => new ItemParaPedido(r.Id, r.Tarefa, r.MaquinaId, r.NomeDaMaquina, r.ComprimentoBruto, r.Data))
                .ToList();

            await _pedidoService.CriarAsync(itens, null);
            PedidoCriado = true;
            LimparSelecao();
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível lançar o pedido: {ex.Message}";
        }
        finally
        {
            LancandoPedido = false;
        }
    }
}
