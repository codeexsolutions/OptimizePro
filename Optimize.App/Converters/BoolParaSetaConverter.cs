using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FluentAvalonia.UI.Controls;

namespace Optimize.App.Converters;

/// <summary>Seta do acordeão de Reposição (§22.7) — para baixo quando aberta, pra direita quando fechada.</summary>
public sealed class BoolParaSetaConverter : IValueConverter
{
    public static readonly BoolParaSetaConverter Instancia = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Symbol.ChevronDown : Symbol.ChevronRight;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
