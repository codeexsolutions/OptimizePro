using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Optimize.App.Converters;

/// <summary>Esmaece a linha de uma máquina desativada na tela de Máquinas (§22.3), sem escondê-la de vez.</summary>
public sealed class BoolParaOpacidadeConverter : IValueConverter
{
    public static readonly BoolParaOpacidadeConverter Instancia = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 1.0 : 0.55;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
