using Avalonia.Controls;
using Optimize.App.ViewModels;

namespace Optimize.App.Views;

public partial class LoginDoPainelWindow : Window
{
    public LoginDoPainelWindow()
    {
        InitializeComponent();
    }

    public LoginDoPainelWindow(LoginDoPainelViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.PropertyChanged += (_, e) =>
        {
            // Fecha sozinha assim que o login der certo — quem chamou (App.axaml.cs) segue
            // pra MainWindow logo depois de esperar esta janela fechar (mesmo padrão de
            // LicencaWindow).
            if (e.PropertyName == nameof(LoginDoPainelViewModel.Autenticado) && viewModel.Autenticado)
                Close();
        };
    }
}
