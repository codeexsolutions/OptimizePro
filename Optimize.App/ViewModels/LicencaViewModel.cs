using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OptimizePro.Services.Licenciamento;

namespace Optimize.App.ViewModels;

/// <summary>
/// Tela de ativação (§ "acesso controlado", 02/09/2026) — porta de entrada ANTES da tela
/// principal: sem código válido, o app não sai daqui. Mostrada uma vez ao abrir; se já existe
/// uma licença válida salva (ativação anterior), o app pula direto pra <c>MainWindow</c> sem
/// nem exibir esta tela (ver <c>App.axaml.cs</c>).
/// </summary>
public sealed partial class LicencaViewModel(LicencaService licenca) : ViewModelBase
{
    [ObservableProperty]
    public partial string Codigo { get; set; } = "";

    [ObservableProperty]
    public partial string? Mensagem { get; set; }

    [ObservableProperty]
    public partial bool MensagemEhErro { get; set; }

    [ObservableProperty]
    public partial bool Ativado { get; set; }

    /// <summary>Chamado pelo código que resolve a tela (§ App.axaml.cs) quando já existia uma licença salva, mas vencida/suspeita — explica o motivo em vez de só pedir "digite o código", sem estado.</summary>
    public void DefinirMotivoDoBloqueio(SituacaoDaLicenca situacao, DateOnly? validoAte)
    {
        Mensagem = situacao switch
        {
            SituacaoDaLicenca.Expirada => $"Sua licença venceu em {validoAte:dd/MM/yyyy}. Informe um código novo pra continuar usando.",
            SituacaoDaLicenca.RelogioSuspeito => "O relógio deste computador parece ter voltado no tempo — por segurança, informe o código de novo pra continuar.",
            _ => null,
        };
    }

    [RelayCommand]
    private void Ativar()
    {
        if (string.IsNullOrWhiteSpace(Codigo))
        {
            Mensagem = "Digite o código de licença recebido.";
            MensagemEhErro = true;
            return;
        }

        var resultado = licenca.Ativar(Codigo);
        Mensagem = resultado.Mensagem;
        MensagemEhErro = !resultado.Sucesso;
        Ativado = resultado.Sucesso;
    }
}
