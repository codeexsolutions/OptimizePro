using Avalonia.Controls;
using Optimize.App.ViewModels;

namespace Optimize.App.Views;

public partial class LicencaWindow : Window
{
    public LicencaWindow()
    {
        InitializeComponent();
    }

    public LicencaWindow(LicencaViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.PropertyChanged += (_, e) =>
        {
            // Fecha sozinha assim que a ativação der certo — quem chamou (App.axaml.cs) segue
            // pra MainWindow logo depois de esperar esta janela fechar.
            if (e.PropertyName == nameof(LicencaViewModel.Ativado) && viewModel.Ativado)
                Close();
        };
    }
}
