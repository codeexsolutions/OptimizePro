using System.Globalization;
using System.Xml.Linq;
using OptimizePro.Core.Impressoras;
using OptimizePro.Data.Entidades;

namespace OptimizePro.Services.Impressoras.Historico;

/// <summary>
/// Porte de <c>impressoras/sources/csvHistory.js</c> (impressora "printer2"/CSV, §22.4).
/// Não porta o estimador de dimensão por BMP de preview (fallback de um fallback, só entra
/// quando o registro é recorte/mosaico E o Joblist.xml não tem a metragem) — se faltar,
/// o registro entra com comprimento/área zerados em vez de estimados por imagem.
/// </summary>
public sealed class LeitorCsvHistorico : ILeitorDeHistorico
{
    private const double MlDeTintaPorM2 = 3;
    private const double PolegadasPorMetro = 39.37;

    public async Task<List<RegistroDeImpressao>> LerIntervaloAsync(Maquina maquina, string dataInicioIso, string dataFimIso, CancellationToken ct = default)
    {
        if (maquina.CaminhoHistorico is null) return [];

        var linhas = (await File.ReadAllLinesAsync(maquina.CaminhoHistorico, ct))
            .Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (linhas.Count > 0) linhas.RemoveAt(0); // cabeçalho

        var metaPorTarefa = await CarregarMetaDoJobListAsync(maquina.CaminhoListaDeTrabalhos, ct);

        var resultado = new List<RegistroDeImpressao>();
        // O PrinterManager repete o mesmo Start para todas as cópias de uma tiragem e grava
        // End/Cost acumulados — pra cada cópia seguinte, o início real é o End da anterior.
        var ultimoFimPorTiragem = new Dictionary<string, DateTime>();

        for (var indice = 0; indice < linhas.Count; indice++)
        {
            var reg = AnalisarLinha(linhas[indice]);
            if (reg is null) continue;

            var inicio = AnalisarDataHoraUs(reg.Value.Inicio);
            if (inicio is null) continue;
            var fim = AnalisarDataHoraUs(reg.Value.Fim);

            var tarefa = Path.GetFileName(reg.Value.NomeDoArquivo);
            var chaveDaTiragem = $"{reg.Value.Inicio}|{tarefa.ToLowerInvariant()}";
            var inicioEfetivo = ultimoFimPorTiragem.GetValueOrDefault(chaveDaTiragem, inicio.Value);
            if (fim is not null) ultimoFimPorTiragem[chaveDaTiragem] = fim.Value;

            var data = inicioEfetivo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (string.Compare(data, dataInicioIso, StringComparison.Ordinal) < 0 ||
                string.Compare(data, dataFimIso, StringComparison.Ordinal) > 0) continue;

            MetaDaTarefa? meta = metaPorTarefa.TryGetValue(tarefa.ToLowerInvariant(), out var metaEncontrada) ? metaEncontrada : null;

            var comprimento = reg.Value.AlturaPolegadas / PolegadasPorMetro;
            var largura = reg.Value.LarguraPolegadas / PolegadasPorMetro;
            var area = comprimento * largura;
            var estimado = false;

            if (reg.Value.EhRecorteOuMosaico && (comprimento <= 0 || largura <= 0) && meta is { ComprimentoM: > 0 })
            {
                comprimento = meta.Value.ComprimentoM;
                largura = meta.Value.LarguraM;
                area = meta.Value.AreaM2;
            }

            var tempoSegundos = fim is not null
                ? Math.Max(0, (int)Math.Round((fim.Value - inicioEfetivo).TotalSeconds))
                : Math.Max(0, (int)Math.Round(reg.Value.Custo));

            resultado.Add(new RegistroDeImpressao
            {
                Id = $"{maquina.Id}|{indice}|{reg.Value.Inicio}|{tarefa}",
                MaquinaId = maquina.Id,
                NomeDaMaquina = maquina.Nome,
                TipoDeOrigem = "csv",
                DataHora = data + " " + inicioEfetivo.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                Data = data,
                Hora = inicioEfetivo.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                Tarefa = tarefa,
                Status = "Concluído",
                Cancelada = false,
                ComErro = false,
                AreaDeImpressao = area,
                ComprimentoDeImpressao = comprimento,
                MetricaEstimada = estimado,
                TempoSegundos = tempoSegundos,
                TintaMl = area > 0 ? area * MlDeTintaPorM2 : 0,
                TintaExperimental = area > 0,
                ReferenciaDePreview = meta?.ReferenciaDePreview ?? tarefa,
                EhRecorteOuMosaico = reg.Value.EhRecorteOuMosaico,
            });
        }

        return resultado;
    }

    public async Task<string> AssinaturaAsync(Maquina maquina, CancellationToken ct = default)
    {
        if (maquina.CaminhoHistorico is null) return "";
        var info = new FileInfo(maquina.CaminhoHistorico);
        var assinatura = $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";

        if (maquina.CaminhoListaDeTrabalhos is not null && File.Exists(maquina.CaminhoListaDeTrabalhos))
        {
            var infoJobList = new FileInfo(maquina.CaminhoListaDeTrabalhos);
            assinatura += $":{infoJobList.Length}:{infoJobList.LastWriteTimeUtc.Ticks}";
        }

        return await Task.FromResult(assinatura);
    }

    private readonly record struct LinhaCsv(string Inicio, string Fim, double Custo, string NomeDoArquivo, bool EhRecorteOuMosaico, double LarguraPolegadas, double AlturaPolegadas);

    private static LinhaCsv? AnalisarLinha(string linha)
    {
        var partes = linha.Split(',');
        if (partes.Length < 7) return null;
        return new LinhaCsv(
            partes[0].Trim(),
            partes[1].Trim(),
            double.TryParse(partes[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var custo) ? custo : 0,
            partes[3].Trim(),
            partes[4].Trim().Equals("true", StringComparison.OrdinalIgnoreCase),
            double.TryParse(partes[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var largura) ? largura : 0,
            double.TryParse(partes[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var altura) ? altura : 0);
    }

    private static readonly string[] FormatosDeDataUs = ["M/d/yyyy h:mm:ss tt"];

    private static DateTime? AnalisarDataHoraUs(string valor)
    {
        var s = valor.Trim();
        if (DateTime.TryParseExact(s, FormatosDeDataUs, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var generica) ? generica : null;
    }

    private readonly record struct MetaDaTarefa(string ReferenciaDePreview, double LarguraM, double ComprimentoM, double AreaM2);

    /// <summary>Porte de <c>loadJobListMeta</c> — só a metragem/preview de cada trabalho, usada quando o CSV não traz a dimensão de um recorte/mosaico.</summary>
    private static async Task<Dictionary<string, MetaDaTarefa>> CarregarMetaDoJobListAsync(string? caminho, CancellationToken ct)
    {
        var meta = new Dictionary<string, MetaDaTarefa>();
        if (caminho is null || !File.Exists(caminho)) return meta;

        try
        {
            var doc = XDocument.Load(caminho);
            foreach (var job in doc.Descendants("UIJob"))
            {
                var strings = job.Elements("string").Select(e => e.Value).ToList();
                var tarefa = Path.GetFileName(strings.ElementAtOrDefault(0) ?? strings.ElementAtOrDefault(1) ?? "");
                if (string.IsNullOrEmpty(tarefa)) continue;

                var fre = job.Descendants("sFreSetting").FirstOrDefault();
                var resX = LerNumero(fre?.Element("nResolutionX"));
                var resY = LerNumero(fre?.Element("nResolutionY"));

                var clip = job.Element("JobClip");
                var inteiros = clip?.Elements("int").Select(e => LerNumero(e)).ToList() ?? [];
                var larguraPontos = inteiros.ElementAtOrDefault(0);
                var alturaPontos = inteiros.ElementAtOrDefault(1);

                var larguraM = resX > 0 && larguraPontos > 0 ? larguraPontos / resX / PolegadasPorMetro : 0;
                var comprimentoM = resY > 0 && alturaPontos > 0 ? alturaPontos / resY / PolegadasPorMetro : 0;
                var referenciaDePreview = strings.FirstOrDefault(s => s.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
                    || s.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                    || s.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                    ?? tarefa;

                meta[tarefa.ToLowerInvariant()] = new MetaDaTarefa(referenciaDePreview, larguraM, comprimentoM, larguraM * comprimentoM);
            }
        }
        catch
        {
            // Joblist.xml ausente/corrompido — segue sem a metragem de recorte/mosaico.
        }

        return await Task.FromResult(meta);
    }

    private static double LerNumero(XElement? elemento) =>
        elemento is not null && double.TryParse(elemento.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
