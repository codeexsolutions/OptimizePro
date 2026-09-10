using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OptimizePro.Services.Impressoras;

/// <summary>Porte de <c>impressoras/utils/text.js</c> + <c>services/matching.js</c> (§22.8) — a convenção da fábrica de nomear o arquivo "CLIENTE - TECIDO".</summary>
public static class TextoDeImpressoras
{
    /// <summary>Porte de <c>normalizeText</c> — sem acento e minúsculo, pra comparação (não é exibido).</summary>
    public static string Normalizar(string valor)
    {
        var normalizado = valor.Normalize(NormalizationForm.FormD);
        var semAcento = new StringBuilder();
        foreach (var c in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                semAcento.Append(c);
        }
        return semAcento.ToString().ToLowerInvariant();
    }

    /// <summary>Porte de <c>parseClientFabric</c> — separadores aceitos: " - ", " – " (traço longo), ou "_" como fallback.</summary>
    public static (string Cliente, string Tecido) SepararClienteETecido(string? nomeDaTarefa)
    {
        var basico = Regex.Replace((nomeDaTarefa ?? "").Trim(), @"\.[a-zA-Z0-9]{2,5}$", "").Trim();
        if (basico.Length == 0) return ("", "");

        var partes = Regex.Split(basico, @"\s+[-–]\s+");
        if (partes.Length < 2) partes = Regex.Split(basico, "_+");

        if (partes.Length >= 2)
            return (partes[0].Trim(), string.Join(" - ", partes.Skip(1)).Trim());

        return (basico, "");
    }
}
