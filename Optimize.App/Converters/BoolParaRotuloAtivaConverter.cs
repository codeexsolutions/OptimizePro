using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Optimize.App.Converters;

public sealed class BoolParaRotuloAtivaConverter : IValueConverter
{
    public static readonly BoolParaRotuloAtivaConverter Instancia = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Ativa" : "Desativada";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
