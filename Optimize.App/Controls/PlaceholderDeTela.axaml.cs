using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace Optimize.App.Controls;

/// <summary>Placeholder visual reaproveitado pelas telas que ainda não têm Services por trás — ícone + título + descrição do que vai entrar ali.</summary>
public partial class PlaceholderDeTela : UserControl
{
    public static readonly StyledProperty<Symbol> IconeProperty =
        AvaloniaProperty.Register<PlaceholderDeTela, Symbol>(nameof(Icone));

    public static readonly StyledProperty<string?> TituloProperty =
        AvaloniaProperty.Register<PlaceholderDeTela, string?>(nameof(Titulo));

    public static readonly StyledProperty<string?> DescricaoProperty =
        AvaloniaProperty.Register<PlaceholderDeTela, string?>(nameof(Descricao));

    public Symbol Icone
    {
        get => GetValue(IconeProperty);
        set => SetValue(IconeProperty, value);
    }

    public string? Titulo
    {
        get => GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public string? Descricao
    {
        get => GetValue(DescricaoProperty);
        set => SetValue(DescricaoProperty, value);
    }

    public PlaceholderDeTela()
    {
        InitializeComponent();
    }
}
