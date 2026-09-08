using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Optimize.App.Converters;

/// <summary>Altura do traço da régua (§9.3) — marca "maior" (a cada 50cm, com rótulo) fica mais comprida que a marca fina intermediária (a cada 10cm).</summary>
public sealed class MaiorParaAlturaDeTracoConverter : IValueConverter
{
    public static readonly MaiorParaAlturaDeTracoConverter Instancia = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 12.0 : 6.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
