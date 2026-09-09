using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace Optimize.App.Controls;

/// <summary>
/// Porte de <c>Cartao.tsx</c> do optmize-full — a caixa em que todo conteúdo de tela mora:
/// título, apoio e ícone opcionais no cabeçalho, mais uma ação (normalmente um botão) na
/// ponta direita. Antes cada tela tinha seu próprio <c>Border</c>+cabeçalho escrito à mão,
/// com espaçamento levemente diferente em cada uma; agora a medida é uma só, igual à
/// referência.
/// </summary>
public partial class Cartao : ContentControl
{
    public static readonly StyledProperty<string?> TituloProperty =
        AvaloniaProperty.Register<Cartao, string?>(nameof(Titulo));

    public static readonly StyledProperty<string?> ApoioProperty =
        AvaloniaProperty.Register<Cartao, string?>(nameof(Apoio));

    public static readonly StyledProperty<IconSource?> IconeProperty =
        AvaloniaProperty.Register<Cartao, IconSource?>(nameof(Icone));

    public static readonly StyledProperty<object?> AcaoProperty =
        AvaloniaProperty.Register<Cartao, object?>(nameof(Acao));

    public string? Titulo
    {
        get => GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public string? Apoio
    {
        get => GetValue(ApoioProperty);
        set => SetValue(ApoioProperty, value);
    }

    public IconSource? Icone
    {
        get => GetValue(IconeProperty);
        set => SetValue(IconeProperty, value);
    }

    /// <summary>O que vai na ponta direita do cabeçalho — normalmente o botão da ação principal da seção.</summary>
    public object? Acao
    {
        get => GetValue(AcaoProperty);
        set => SetValue(AcaoProperty, value);
    }

    public Cartao()
    {
        InitializeComponent();
    }
}
