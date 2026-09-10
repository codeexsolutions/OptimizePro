using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OptimizePro.Data.Entidades;

namespace OptimizePro.Services.Impressoras.Historico;

/// <summary>Porte de <c>impressoras/sources/xmlHistory.js</c> (PrinterManager XML, §22.4) — uma pasta por dia (AAAA/AAAAMM/AAAAMMDD), um XML por trabalho.</summary>
public sealed class LeitorXmlHistorico : ILeitorDeHistorico
{
    private const double MlDeTintaPorM2 = 3;

    public async Task<List<RegistroDeImpressao>> LerIntervaloAsync(Maquina maquina, string dataInicioIso, string dataFimIso, CancellationToken ct = default)
    {
        if (maquina.CaminhoHistorico is null) return [];
        var resultado = new List<RegistroDeImpressao>();

        foreach (var dia in DatasHistorico.EnumerarDias(dataInicioIso, dataFimIso))
        {
            var pasta = PastaDoDia(maquina.CaminhoHistorico, dia);
            if (!Directory.Exists(pasta)) continue;

            foreach (var arquivo in Directory.GetFiles(pasta, "*.xml"))
            {
                List<XElement> registros;
                try
                {
                    var doc = await Task.Run(() => XDocument.Load(arquivo), ct);
                    registros = doc.Descendants("PrintRecord").ToList();
                }
                catch
                {
                    continue; // XML corrompido/ilegível — pula, igual à referência (só loga e segue).
                }

                for (var indice = 0; indice < registros.Count; indice++)
                {
                    var registro = registros[indice];
                    var job = registro.Element("UIJob");
                    var tarefa = job?.Element("string")?.Value ?? Path.GetFileName(arquivo);
                    var dataHoraBruta = job?.Element("dateTime")?.Value ?? "";
                    var (dataHora, data, hora) = NormalizarDataHora(dataHoraBruta, dia);
                    var statusBruto = job?.Element("JobStatus")?.Value ?? "Concluído";

                    var floats = registro.Elements("float").Select(e => AnalisarDouble(e.Value)).ToList();
                    var comprimento = floats.ElementAtOrDefault(0);
                    var area = floats.ElementAtOrDefault(1);

                    var ticks = long.TryParse(registro.Element("long")?.Value, out var t) ? t : 0;
                    var tempoSegundos = ticks > 0 ? (int)Math.Round(ticks / 10_000_000.0) : 0;

                    var tintaMl = area > 0 ? area * MlDeTintaPorM2 : 0;
                    var comErro = Regex.IsMatch(statusBruto, "error|fail", RegexOptions.IgnoreCase) || comprimento == 0;

                    var stringsDoJob = job?.Elements("string").Select(e => e.Value).ToList() ?? [];
                    var referenciaDePreview = stringsDoJob.FirstOrDefault(s => Regex.IsMatch(s, @"\.(bmp|jpg|jpeg|png)$", RegexOptions.IgnoreCase)) ?? tarefa;

                    resultado.Add(new RegistroDeImpressao
                    {
                        Id = $"{maquina.Id}|{dataHora}|{tarefa}|{indice}",
                        MaquinaId = maquina.Id,
                        NomeDaMaquina = maquina.Nome,
                        TipoDeOrigem = "xml",
                        DataHora = dataHora,
                        Data = string.IsNullOrEmpty(data) ? dia : data,
                        Hora = hora,
                        Tarefa = tarefa,
                        Status = comErro ? "Erro" : statusBruto,
                        Cancelada = false,
                        ComErro = comErro,
                        AreaDeImpressao = area,
                        ComprimentoDeImpressao = comprimento,
                        TempoSegundos = tempoSegundos,
                        TintaMl = tintaMl,
                        TintaExperimental = tintaMl > 0,
                        ReferenciaDePreview = referenciaDePreview,
                    });
                }
            }
        }

        return resultado;
    }

    public Task<string> AssinaturaAsync(Maquina maquina, CancellationToken ct = default)
    {
        if (maquina.CaminhoHistorico is null) return Task.FromResult("");
        var hoje = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var pasta = PastaDoDia(maquina.CaminhoHistorico, hoje);
        if (!Directory.Exists(pasta)) return Task.FromResult("0:0");

        var arquivos = Directory.GetFiles(pasta, "*.xml");
        var maisRecente = arquivos.Length > 0 ? arquivos.Max(a => new FileInfo(a).LastWriteTimeUtc.Ticks) : 0;
        return Task.FromResult($"{arquivos.Length}:{maisRecente}");
    }

    private static string PastaDoDia(string raiz, string dataIso)
    {
        var partes = dataIso.Split('-');
        return Path.Combine(raiz, partes[0], partes[0] + partes[1], partes[0] + partes[1] + partes[2]);
    }

    private static (string DataHora, string Data, string Hora) NormalizarDataHora(string bruta, string diaDeReserva)
    {
        if (string.IsNullOrEmpty(bruta)) return ("", "", "");
        var partes = bruta.Split('T');
        var data = partes[0];
        var hora = partes.Length > 1 ? partes[1].Length > 8 ? partes[1][..8] : partes[1] : "";
        return ($"{data} {hora}".Trim(), data, hora);
    }

    private static double AnalisarDouble(string? valor) =>
        double.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
