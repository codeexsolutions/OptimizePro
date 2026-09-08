using System;
using System.Globalization;
using Avalonia.Data.Converters;
using OptimizePro.Core.Encaixe;

namespace Optimize.App.Converters;

/// <summary>Rótulo em PT-BR pro seletor de giro (§11.11) — evita <c>enum.ToString()</c> cru na tela.</summary>
public sealed class TipoDeGiroParaRotuloConverter : IValueConverter
{
    public static readonly TipoDeGiroParaRotuloConverter Instancia = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        null => "(padrão — segue a config. geral)",
        TipoDeGiro.MantemSentido => "Mantém sentido (180°)",
        TipoDeGiro.Fixa => "Fixa (não gira)",
        TipoDeGiro.Livre => "Livre (90°)",
        _ => value.ToString(),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
