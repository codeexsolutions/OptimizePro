using System;
using Microsoft.Extensions.DependencyInjection;
using Optimize.App.ViewModels;

namespace Optimize.App.Services;

public sealed class FabricaDeViewModels(IServiceProvider servicos) : IFabricaDeViewModels
{
    public ViewModelBase Criar(TipoDeTela tipo, object? parametro = null)
    {
        var viewModel = tipo switch
        {
            TipoDeTela.Moldes => (ViewModelBase)servicos.GetRequiredService<MoldesViewModel>(),
            TipoDeTela.MoldeWizard => servicos.GetRequiredService<MoldeWizardViewModel>(),
            TipoDeTela.Projetos => servicos.GetRequiredService<ProjetosViewModel>(),
            TipoDeTela.ProjetoEditor => servicos.GetRequiredService<ProjetoEditorViewModel>(),
            TipoDeTela.Encaixe => servicos.GetRequiredService<EncaixeViewModel>(),
            TipoDeTela.Vetor => servicos.GetRequiredService<VetorViewModel>(),
            TipoDeTela.Disparo => servicos.GetRequiredService<DisparoViewModel>(),
            TipoDeTela.Configuracoes => servicos.GetRequiredService<ConfiguracoesViewModel>(),
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
        };

        if (viewModel is IRecebeParametro recebedor)
            recebedor.Receber(parametro);

        return viewModel;
    }
}
