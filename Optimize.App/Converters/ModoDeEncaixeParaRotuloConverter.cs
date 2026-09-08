using System;
using System.Globalization;
using Avalonia.Data.Converters;
using OptimizePro.Services.Encaixe;

namespace Optimize.App.Converters;

/// <summary>Rótulo em PT-BR pro seletor "Como encaixar" — porte do texto do FAQ original ("Qual jeito de encaixar escolher?").</summary>
public sealed class ModoDeEncaixeParaRotuloConverter : IValueConverter
{
    public static readonly ModoDeEncaixeParaRotuloConverter Instancia = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ModoDeEncaixe.Automatico => "Automático: rápido e econômico",
        ModoDeEncaixe.SempreContorno => "Sempre pelo contorno",
        ModoDeEncaixe.SempreCaixa => "Sempre pela caixa",
        _ => value?.ToString(),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
