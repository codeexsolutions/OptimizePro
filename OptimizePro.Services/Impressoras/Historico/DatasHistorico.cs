using System.Globalization;

namespace OptimizePro.Services.Impressoras.Historico;

/// <summary>Porte de <c>impressoras/utils/date.js</c> (só o que os leitores de histórico usam) — datas sempre como string ISO "yyyy-MM-dd", comparáveis léxico igual no SQLite.</summary>
public static class DatasHistorico
{
    private const string Formato = "yyyy-MM-dd";

    public static string SomarDias(string dataIso, int dias) =>
        DateTime.ParseExact(dataIso, Formato, CultureInfo.InvariantCulture).AddDays(dias).ToString(Formato, CultureInfo.InvariantCulture);

    public static IEnumerable<string> EnumerarDias(string inicioIso, string fimIso)
    {
        var atual = DateTime.ParseExact(inicioIso, Formato, CultureInfo.InvariantCulture);
        var fim = DateTime.ParseExact(fimIso, Formato, CultureInfo.InvariantCulture);
        while (atual <= fim)
        {
            yield return atual.ToString(Formato, CultureInfo.InvariantCulture);
            atual = atual.AddDays(1);
        }
    }
}
