using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Optimize.App.Converters;

/// <summary>Bolinha de status do painel Impressoras (§22.6): verde on-line, vermelho off-line, cinza "ainda não se sabe".</summary>
public sealed class StatusOnlineParaCorConverter : IValueConverter
{
    public static readonly StatusOnlineParaCorConverter Instancia = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        true => Brushes.LimeGreen,
        false => Brushes.IndianRed,
        _ => Brushes.Gray,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
