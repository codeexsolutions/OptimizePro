using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Optimize.App.Services;
using OptimizePro.Services.Impressoras;
using OptimizePro.Services.Impressoras.Historico;

namespace Optimize.App.ViewModels;

/// <summary>
/// Painel Impressoras (§22.6) — o dashboard ao vivo, porte reduzido de <c>Impressoras.tsx</c>:
/// um cartão por máquina com status on-line/off-line, trabalhos e metragem de hoje, e o último
/// trabalho. Atualiza sozinho quando o <see cref="PollingDeImpressorasService"/> embutido no
/// servidor avisa (via SignalR) que algo mudou — sem ninguém precisar apertar nada, igual à
/// referência.
/// </summary>
public partial class ImpressorasViewModel : ViewModelBase
{
    private readonly IMaquinaService _maquinaService;
    private readonly IHistoricoService _historicoService;
    private readonly ClientePainelEmTempoReal _cliente;

    public ObservableCollection<ImpressoraCardItem> Impressoras { get; } = [];

    public bool TemImpressoras => Impressoras.Count > 0;

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? MensagemDeErro { get; set; }

    public ImpressorasViewModel(IMaquinaService maquinaService, IHistoricoService historicoService, ClientePainelEmTempoReal cliente)
    {
        _maquinaService = maquinaService;
        _historicoService = historicoService;
        _cliente = cliente;

        Impressoras.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TemImpressoras));
        _cliente.EventoRecebido += OnEventoRecebido;

        _ = _cliente.GarantirConectadoAsync();
        _ = CarregarAsync();
    }

    private void OnEventoRecebido(string evento) => Dispatcher.UIThread.Post(() => _ = CarregarAsync());

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Carregando = true;
        MensagemDeErro = null;
        try
        {
            var maquinas = await _maquinaService.ListarAsync(incluirDesabilitadas: false);
            var hoje = DateTime.Now.ToString("yyyy-MM-dd");
            var resposta = await _historicoService.ObterAsync(null, hoje, hoje);
            var status = await _cliente.ObterStatusAsync();

            var registrosPorMaquina = resposta.Registros.GroupBy(r => r.MaquinaId).ToDictionary(g => g.Key, g => g.ToList());

            Impressoras.Clear();
            foreach (var maquina in maquinas)
            {
                var registros = registrosPorMaquina.GetValueOrDefault(maquina.Id, []);
                var ultimo = registros.OrderByDescending(r => r.DataHora).FirstOrDefault();
                var online = status.TryGetValue(maquina.Id, out var estado) ? estado.Online : (bool?)null;

                Impressoras.Add(new ImpressoraCardItem
                {
                    Id = maquina.Id,
                    Nome = maquina.Nome,
                    RotuloDoTipo = VarreduraDeRedeService.RotuloDoTipo(maquina.Tipo),
                    Online = online,
                    TrabalhosHoje = registros.Count,
                    MetragemHoje = FormatoImpressoras.MetrosCurtos(registros.Sum(r => r.ComprimentoDeImpressao)),
                    UltimoTrabalho = ultimo?.Tarefa,
                    UltimoHorario = ultimo is null ? null : $"{FormatoImpressoras.DataBr(ultimo.Data)} {ultimo.Hora}",
                });
            }
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível carregar o painel: {ex.Message}";
        }
        finally
        {
            Carregando = false;
        }
    }
}
