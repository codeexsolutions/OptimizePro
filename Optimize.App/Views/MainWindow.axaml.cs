using System;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Optimize.App.ViewModels;

namespace Optimize.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Glue mínimo: o NavigationView não é data-bindable o bastante para expor seleção por
    // enum diretamente sem um conversor a mais — mapeia a Tag do item pra TipoDeTela e
    // delega pro comando do ViewModel (§7.1 da arquitetura: só o glue necessário).
    private void NavPrincipal_OnSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (e.SelectedItem is not NavigationViewItem { Tag: string tag }) return;

        if (Enum.TryParse<TipoDeTela>(tag, out var tipo))
            vm.NavegarParaTipoCommand.Execute(tipo);
    }
}
