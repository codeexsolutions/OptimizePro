namespace Optimize.App.ViewModels;

public enum TipoDeTela
{
    Moldes,
    MoldeWizard,
    Projetos,
    ProjetoEditor,
    Encaixe,
    Vetor,
    Disparo,
    Configuracoes,
}

public sealed record ItemDeMenu(TipoDeTela Tipo, string Titulo, string Secao);
