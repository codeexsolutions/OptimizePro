using System.Text;
using System.Text.RegularExpressions;

namespace OptimizePro.Core.Moldes.Pdf;

/// <summary>
/// Varre o arquivo inteiro por <c>N G obj</c> (§8.1: "sem usar a tabela xref... robusto a
/// xref corrompido"), em vez de seguir a xref/trailer. Em atualização incremental, o
/// último <c>obj</c> de cada número no arquivo é o que vale (sobrescreve o anterior no
/// dicionário, na ordem natural da varredura). Depois expande Object Streams (ObjStm).
/// </summary>
public static partial class PdfAnalisadorDeDocumento
{
    [GeneratedRegex(@"(?<num>\d+)\s+(?<gen>\d+)\s+obj\b")]
    private static partial Regex RegexObjeto();

    public static PdfDocumento Analisar(string texto, List<string> avisos)
    {
        var doc = new PdfDocumento();

        foreach (Match m in RegexObjeto().Matches(texto))
        {
            var num = int.Parse(m.Groups["num"].Value);
            var pos = m.Index + m.Length;

            var valor = PdfAnalisadorDeObjetos.AnalisarValor(texto, ref pos);

            if (valor is Dictionary<string, object?> dict)
            {
                var streamBytes = TentarExtrairStream(texto, dict, ref pos);
                doc.Objetos[num] = (dict, streamBytes);
            }
            else if (valor is List<object?> lista)
            {
                doc.Objetos[num] = (new Dictionary<string, object?> { ["__valor__"] = lista }, null);
            }
            // números/nomes/strings soltos como objeto de topo não interessam ao pipeline.
        }

        ExpandirObjectStreams(doc, avisos);

        return doc;
    }

    /// <summary>
    /// Se o dicionário for seguido por <c>stream</c>, captura os bytes crus até
    /// <c>endstream</c> (ignora <c>/Length</c> de propósito — mais robusto contra xref
    /// quebrada do que confiar num valor que pode estar errado ou ser uma referência
    /// não resolvida ainda).
    /// </summary>
    private static byte[]? TentarExtrairStream(string texto, Dictionary<string, object?> dict, ref int pos)
    {
        var p = pos;
        PdfAnalisadorDeObjetos.PularEspacosEComentarios(texto, ref p);

        if (p + 6 > texto.Length || string.CompareOrdinal(texto, p, "stream", 0, 6) != 0)
            return null;

        var inicio = p + 6;
        if (inicio < texto.Length && texto[inicio] == '\r') inicio++;
        if (inicio < texto.Length && texto[inicio] == '\n') inicio++;

        var fim = texto.IndexOf("endstream", inicio, StringComparison.Ordinal);
        if (fim < 0) fim = texto.Length;

        var fimReal = fim;
        if (fimReal > inicio && texto[fimReal - 1] == '\n') fimReal--;
        if (fimReal > inicio && texto[fimReal - 1] == '\r') fimReal--;

        pos = fim;
        return Encoding.Latin1.GetBytes(texto.Substring(inicio, Math.Max(0, fimReal - inicio)));
    }

    private static void ExpandirObjectStreams(PdfDocumento doc, List<string> avisos)
    {
        foreach (var chave in doc.Objetos.Keys.ToList())
        {
            var (dict, stream) = doc.Objetos[chave];
            if (stream is null || PdfDocumento.NomeDoTipo(dict) != "ObjStm")
                continue;

            byte[] decodificado;
            try
            {
                decodificado = PdfFiltros.Decodificar(dict, stream);
            }
            catch (Exception ex)
            {
                avisos.Add($"Falha ao decodificar ObjStm {chave}: {ex.Message}");
                continue;
            }

            var textoObjStm = Encoding.Latin1.GetString(decodificado);
            var n = PdfFiltros.ObterInt(dict, "N") ?? 0;
            var first = PdfFiltros.ObterInt(dict, "First") ?? 0;

            var posCabecalho = 0;
            var pares = new List<(int Num, int Offset)>(n);
            for (var i = 0; i < n; i++)
            {
                var num = (int)PdfAnalisadorDeObjetos.LerNumero(textoObjStm, ref posCabecalho);
                var offset = (int)PdfAnalisadorDeObjetos.LerNumero(textoObjStm, ref posCabecalho);
                pares.Add((num, offset));
            }

            foreach (var (num, offset) in pares)
            {
                var posObjeto = first + offset;
                if (posObjeto < 0 || posObjeto >= textoObjStm.Length)
                    continue;

                var valor = PdfAnalisadorDeObjetos.AnalisarValor(textoObjStm, ref posObjeto);
                if (valor is Dictionary<string, object?> subDict)
                    doc.Objetos[num] = (subDict, null);
            }
        }
    }
}
