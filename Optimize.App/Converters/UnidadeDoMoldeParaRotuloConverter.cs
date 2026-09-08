using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Optimize.App.Converters;

/// <summary>Rótulo em PT-BR pro seletor "Unidade do molde" (null = detecta do próprio arquivo).</summary>
public sealed class UnidadeDoMoldeParaRotuloConverter : IValueConverter
{
    public static readonly UnidadeDoMoldeParaRotuloConverter Instancia = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value as string ?? "Automático";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
