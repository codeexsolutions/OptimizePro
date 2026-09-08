using System;
using System.Globalization;
using Avalonia.Data.Converters;
using OptimizePro.Core.Moldes;

namespace Optimize.App.Converters;

/// <summary>Rótulo em PT-BR pro seletor "Como ler o vetor" (§8.1 — marcador × arquivo inteiro).</summary>
public sealed class ModoLeituraVetorParaRotuloConverter : IValueConverter
{
    public static readonly ModoLeituraVetorParaRotuloConverter Instancia = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ModoLeituraVetor.Marcador => "Marcador: cada peça separada",
        ModoLeituraVetor.Inteiro => "Arquivo inteiro: uma peça só",
        _ => value?.ToString(),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
