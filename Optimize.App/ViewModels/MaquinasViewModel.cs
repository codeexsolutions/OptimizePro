using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OptimizePro.Services.Impressoras;

namespace Optimize.App.ViewModels;

/// <summary>
/// Tela de cadastro de Máquinas (§22.3) — lista as impressoras já cadastradas e permite
/// varrer a rede local pra achar novas (porte de <c>routes/machines.js</c> + a tela
/// correspondente do optmize-full).
/// </summary>
public partial class MaquinasViewModel : ViewModelBase
{
    private readonly IMaquinaService _maquinaService;

    public ObservableCollection<MaquinaItem> Maquinas { get; } = [];
    public ObservableCollection<MaquinaPendenteItem> Pendentes { get; } = [];

    public bool TemMaquinas => Maquinas.Count > 0;
    public bool TemPendentes => Pendentes.Count > 0;

    [ObservableProperty]
    public partial bool VarrendoARede { get; set; }

    [ObservableProperty]
    public partial string FaseDaVarredura { get; set; } = "idle";

    [ObservableProperty]
    public partial int Escaneados { get; set; }

    [ObservableProperty]
    public partial int Total { get; set; }

    [ObservableProperty]
    public partial string MensagemDaVarredura { get; set; } = "Nenhuma varredura executada ainda";

    [ObservableProperty]
    public partial bool IncluirDesabilitadas { get; set; }

    [ObservableProperty]
    public partial string? MensagemDeErro { get; set; }

    public MaquinasViewModel(IMaquinaService maquinaService)
    {
        _maquinaService = maquinaService;
        Maquinas.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TemMaquinas));
        Pendentes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TemPendentes));
        _maquinaService.VarreduraAtualizada += OnVarreduraAtualizada;
        AtualizarComEstado(_maquinaService.ObterEstadoDaVarredura());
        _ = CarregarAsync();
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        MensagemDeErro = null;
        try
        {
            var maquinas = await _maquinaService.ListarAsync(IncluirDesabilitadas);
            Maquinas.Clear();
            foreach (var maquina in maquinas) Maquinas.Add(new MaquinaItem(maquina));
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível carregar as máquinas: {ex.Message}";
        }
    }

    [RelayCommand]
    private void VarrerRede()
    {
        MensagemDeErro = null;
        if (!_maquinaService.IniciarVarredura([]))
            MensagemDeErro = "Já existe uma varredura em andamento.";
    }

    [RelayCommand]
    private void PararVarredura() => _maquinaService.PararVarredura();

    [RelayCommand]
    private async Task CadastrarAsync(MaquinaPendenteItem pendente)
    {
        if (string.IsNullOrWhiteSpace(pendente.NomeSugerido))
        {
            MensagemDeErro = "Dê um nome para a máquina antes de cadastrar.";
            return;
        }

        try
        {
            var maquina = await _maquinaService.CadastrarPendenteAsync(pendente.Host, pendente.NomeSugerido.Trim());
            if (maquina is null)
            {
                MensagemDeErro = "Essa máquina não está mais pendente — rode a varredura de novo.";
                return;
            }

            Pendentes.Remove(pendente);
            Maquinas.Add(new MaquinaItem(maquina));
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível cadastrar: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Descartar(MaquinaPendenteItem pendente)
    {
        if (_maquinaService.DescartarPendente(pendente.Host))
            Pendentes.Remove(pendente);
    }

    [RelayCommand]
    private async Task DesativarAsync(MaquinaItem maquina)
    {
        if (await _maquinaService.DesativarAsync(maquina.Id))
            await CarregarAsync();
    }

    [RelayCommand]
    private async Task ReativarAsync(MaquinaItem maquina)
    {
        if (await _maquinaService.ReativarAsync(maquina.Id))
            await CarregarAsync();
    }

    [RelayCommand]
    private async Task ExcluirAsync(MaquinaItem maquina)
    {
        var (ok, erro) = await _maquinaService.ExcluirAsync(maquina.Id);
        if (ok) await CarregarAsync();
        else MensagemDeErro = erro;
    }

    partial void OnIncluirDesabilitadasChanged(bool value) => _ = CarregarAsync();

    private void OnVarreduraAtualizada(EstadoDaVarredura estado) =>
        Dispatcher.UIThread.Post(() => AtualizarComEstado(estado));

    private void AtualizarComEstado(EstadoDaVarredura estado)
    {
        VarrendoARede = estado.EmExecucao;
        FaseDaVarredura = estado.Fase;
        Escaneados = estado.Escaneados;
        Total = estado.Total;
        MensagemDaVarredura = estado.Mensagem;

        var hostsPendentesNaTela = Pendentes.Select(p => p.Host).ToHashSet();
        var hostsPendentesNoEstado = estado.Resultados.Where(r => r.Acao == "pendente").Select(r => r.Host).ToHashSet();

        foreach (var host in hostsPendentesNaTela.Except(hostsPendentesNoEstado).ToList())
        {
            var item = Pendentes.FirstOrDefault(p => p.Host == host);
            if (item is not null) Pendentes.Remove(item);
        }

        foreach (var resultado in estado.Resultados.Where(r => r.Acao == "pendente"))
        {
            if (Pendentes.Any(p => p.Host == resultado.Host)) continue;
            Pendentes.Add(new MaquinaPendenteItem
            {
                Host = resultado.Host,
                Ip = resultado.Ip,
                Tipo = resultado.Tipo,
                RotuloDoTipo = resultado.RotuloDoTipo,
                NomeSugerido = resultado.Host,
            });
        }

        if (estado.Fase == "done" && !estado.EmExecucao)
            _ = CarregarAsync();
    }
}
