using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OptimizePro.Services.Impressoras;

namespace Optimize.App.ViewModels;

/// <summary>
/// Tela Pedidos (§22.8) — porte de <c>Pedidos.tsx</c>: a fila da calandra. Quem fecha o ciclo de
/// cada item (calandraStatus) não é esta tela — é o aparelho da calandra, lendo o QR da folha
/// (ver <c>ServidorDoPainel</c>'s <c>/api/scan</c> e <c>/api/impressoras/pedidos/.../resultado</c>).
/// Esta tela só mostra o que ele marcou, muda o andamento do pedido e reimprime a folha.
/// </summary>
public partial class PedidosViewModel : ViewModelBase
{
    private readonly IPedidoService _pedidoService;
    private readonly GeradorDeFolhaDePedido _geradorDeFolha;

    public ObservableCollection<PedidoResumoItem> Pedidos { get; } = [];

    public bool TemPedidos => Pedidos.Count > 0;

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? MensagemDeErro { get; set; }

    public PedidosViewModel(IPedidoService pedidoService, GeradorDeFolhaDePedido geradorDeFolha)
    {
        _pedidoService = pedidoService;
        _geradorDeFolha = geradorDeFolha;
        Pedidos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TemPedidos));
        _ = CarregarAsync();
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Carregando = true;
        MensagemDeErro = null;
        try
        {
            var resumos = await _pedidoService.ListarAsync();
            Pedidos.Clear();
            foreach (var resumo in resumos) Pedidos.Add(new PedidoResumoItem(resumo));
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível carregar os pedidos: {ex.Message}";
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private async Task AlternarAsync(PedidoResumoItem pedido)
    {
        pedido.Aberto = !pedido.Aberto;
        if (!pedido.Aberto || pedido.Itens.Count > 0) return;

        pedido.CarregandoDetalhe = true;
        try
        {
            var completo = await _pedidoService.ObterAsync(pedido.Id);
            if (completo is null) return;

            pedido.Itens.Clear();
            foreach (var item in completo.Itens.OrderBy(i => i.Posicao))
                pedido.Itens.Add(new PedidoItemItem(item));
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível carregar o pedido: {ex.Message}";
        }
        finally
        {
            pedido.CarregandoDetalhe = false;
        }
    }

    [RelayCommand]
    private Task MarcarAbertoAsync(PedidoResumoItem pedido) => MudarStatusAsync(pedido, "aberto");

    [RelayCommand]
    private Task MarcarPausadoAsync(PedidoResumoItem pedido) => MudarStatusAsync(pedido, "pausado");

    [RelayCommand]
    private Task MarcarConcluidoAsync(PedidoResumoItem pedido) => MudarStatusAsync(pedido, "concluido");

    private async Task MudarStatusAsync(PedidoResumoItem pedido, string status)
    {
        try
        {
            if (await _pedidoService.AtualizarStatusAsync(pedido.Id, status))
                pedido.AtualizarStatus(status);
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível mudar o andamento: {ex.Message}";
        }
    }

    [RelayCommand]
    private void PedirConfirmacaoDeExclusao(PedidoResumoItem pedido) => pedido.ConfirmandoExclusao = true;

    [RelayCommand]
    private void CancelarExclusao(PedidoResumoItem pedido) => pedido.ConfirmandoExclusao = false;

    [RelayCommand]
    private async Task ExcluirAsync(PedidoResumoItem pedido)
    {
        try
        {
            if (await _pedidoService.ExcluirAsync(pedido.Id))
                Pedidos.Remove(pedido);
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível excluir o pedido: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ReimprimirFolhaAsync(PedidoResumoItem pedido)
    {
        try
        {
            var completo = await _pedidoService.ObterAsync(pedido.Id);
            if (completo is null) return;

            var html = _geradorDeFolha.GerarHtml(completo);
            var caminho = Path.Combine(Path.GetTempPath(), $"pedido-{pedido.Id[..8]}.html");
            await File.WriteAllTextAsync(caminho, html);

            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível gerar a folha: {ex.Message}";
        }
    }
}
