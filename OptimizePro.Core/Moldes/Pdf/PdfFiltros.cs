using System.IO.Compression;

namespace OptimizePro.Core.Moldes.Pdf;

/// <summary>Decodificação de stream PDF: FlateDecode + preditor PNG (§8.1).</summary>
public static class PdfFiltros
{
    public static byte[] Decodificar(Dictionary<string, object?> dict, byte[] dadosBrutos)
    {
        var filtro = ObterNomeOuPrimeiro(dict, "Filter");
        if (filtro is null)
            return dadosBrutos;

        if (filtro != "FlateDecode")
            throw new NotSupportedException($"Filtro de stream não suportado: {filtro}");

        var descomprimido = InflateZlib(dadosBrutos);

        var parametros = ObterDicionarioOuPrimeiro(dict, "DecodeParms") ?? ObterDicionarioOuPrimeiro(dict, "DP");
        if (parametros is not null)
            descomprimido = AplicarPredictor(descomprimido, parametros);

        return descomprimido;
    }

    private static byte[] InflateZlib(byte[] dados)
    {
        using var origem = new MemoryStream(dados);
        using var zlib = new ZLibStream(origem, CompressionMode.Decompress);
        using var destino = new MemoryStream();
        zlib.CopyTo(destino);
        return destino.ToArray();
    }

    private static byte[] AplicarPredictor(byte[] dados, Dictionary<string, object?> parametros)
    {
        var predictor = ObterInt(parametros, "Predictor") ?? 1;
        if (predictor <= 1)
            return dados;

        if (predictor == 2)
            return dados; // TIFF predictor — raro em PDF de molde; não implementado.

        var cores = ObterInt(parametros, "Colors") ?? 1;
        var bitsPorComponente = ObterInt(parametros, "BitsPerComponent") ?? 8;
        var colunas = ObterInt(parametros, "Columns") ?? 1;

        var bytesPorPixel = Math.Max(1, cores * bitsPorComponente / 8);
        var bytesPorLinha = (cores * bitsPorComponente * colunas + 7) / 8;
        var linhaTamanho = bytesPorLinha + 1;
        if (linhaTamanho <= 0 || dados.Length < linhaTamanho)
            return dados;

        var numLinhas = dados.Length / linhaTamanho;
        var resultado = new byte[numLinhas * bytesPorLinha];
        var anterior = new byte[bytesPorLinha];

        for (var linha = 0; linha < numLinhas; linha++)
        {
            var offsetOrigem = linha * linhaTamanho;
            var tipoFiltro = dados[offsetOrigem];
            var atual = new byte[bytesPorLinha];
            Array.Copy(dados, offsetOrigem + 1, atual, 0, bytesPorLinha);

            for (var i = 0; i < bytesPorLinha; i++)
            {
                var esquerda = i >= bytesPorPixel ? atual[i - bytesPorPixel] : (byte)0;
                var cima = anterior[i];
                var cimaEsquerda = i >= bytesPorPixel ? anterior[i - bytesPorPixel] : (byte)0;

                atual[i] = tipoFiltro switch
                {
                    0 => atual[i],
                    1 => (byte)(atual[i] + esquerda),
                    2 => (byte)(atual[i] + cima),
                    3 => (byte)(atual[i] + (esquerda + cima) / 2),
                    4 => (byte)(atual[i] + PaethPredictor(esquerda, cima, cimaEsquerda)),
                    _ => atual[i],
                };
            }

            Array.Copy(atual, 0, resultado, linha * bytesPorLinha, bytesPorLinha);
            anterior = atual;
        }

        return resultado;
    }

    private static byte PaethPredictor(byte a, byte b, byte c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc) return b;
        return c;
    }

    private static string? ObterNomeOuPrimeiro(Dictionary<string, object?> dict, string chave)
    {
        if (!dict.TryGetValue(chave, out var v)) return null;
        return v switch
        {
            string s => s,
            List<object?> lista when lista.Count > 0 && lista[0] is string s2 => s2,
            _ => null,
        };
    }

    private static Dictionary<string, object?>? ObterDicionarioOuPrimeiro(Dictionary<string, object?> dict, string chave)
    {
        if (!dict.TryGetValue(chave, out var v)) return null;
        return v switch
        {
            Dictionary<string, object?> d => d,
            List<object?> lista when lista.Count > 0 && lista[0] is Dictionary<string, object?> d2 => d2,
            _ => null,
        };
    }

    internal static int? ObterInt(Dictionary<string, object?> dict, string chave)
    {
        if (!dict.TryGetValue(chave, out var v)) return null;
        return v switch { double d => (int)d, _ => null };
    }
}
