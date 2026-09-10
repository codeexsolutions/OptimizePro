using System.Globalization;

namespace OptimizePro.Services.Impressoras.Historico;

/// <summary>Porte de <c>impressoras/formato.ts</c> — os mesmos números formatados do mesmo jeito em toda tela (§22.5).</summary>
public static class FormatoImpressoras
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Metros(double valor) => $"{valor.ToString("N2", PtBr)} m";

    public static string MetrosCurtos(double valor) =>
        valor >= 1000 ? $"{Math.Round(valor).ToString("N0", PtBr)} m" : Metros(valor);

    public static string Duracao(int segundos)
    {
        var total = Math.Max(0, segundos);
        if (total == 0) return "—";
        if (total < 60) return "menos de 1 min";
        var horas = total / 3600;
        var minutos = (int)Math.Round((total % 3600) / 60.0);
        if (horas == 0) return $"{minutos} min";
        return minutos != 0 ? $"{horas} h {minutos} min" : $"{horas} h";
    }

    public static string Tinta(double ml)
    {
        if (ml == 0) return "—";
        return ml >= 1000 ? $"{(ml / 1000).ToString("N1", PtBr)} L" : $"{ml.ToString("N0", PtBr)} mL";
    }

    public static string DataBr(string? iso)
    {
        if (string.IsNullOrEmpty(iso)) return "";
        var partes = iso.Split('-');
        return partes.Length == 3 ? $"{partes[2]}/{partes[1]}/{partes[0]}" : iso;
    }
}
