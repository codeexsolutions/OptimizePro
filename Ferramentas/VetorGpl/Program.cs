using System.Globalization;
using System.Text.Json;
using BitmapToVector;
using BitmapToVector.SkiaSharp;
using SkiaSharp;

// ============================================================================
// GPL-3.0-or-later (via BitmapToVector/Potrace) — ver README.md nesta pasta pro porquê
// disto ser um executável SEPARADO, nunca referenciado in-process pelo app principal.
// ============================================================================

if (args.Length == 0 || args[0] != "trace")
{
    Console.Error.WriteLine("Uso: VetorGpl trace --entrada mascara.png [--turdsize 2] [--alphamax 1.0] [--opttolerance 0.2]");
    return 1;
}

var entrada = LerOpcao(args, "--entrada");
if (entrada is null || !File.Exists(entrada))
{
    Console.Error.WriteLine($"Arquivo de entrada não encontrado: {entrada}");
    return 1;
}

try
{
    using var bitmap = SKBitmap.Decode(entrada);
    if (bitmap is null)
    {
        Console.Error.WriteLine($"Não foi possível decodificar a imagem: {entrada}");
        return 1;
    }

    var param = new PotraceParam
    {
        TurdSize = (int)LerOpcaoDouble(args, "--turdsize", 2),
        AlphaMax = LerOpcaoDouble(args, "--alphamax", 1.0),
        OptTolerance = LerOpcaoDouble(args, "--opttolerance", 0.2),
        OptiCurve = true,
    };

    var caminhos = PotraceSkiaSharp.Trace(param, bitmap);
    var comandosD = caminhos.Select(p => p.ToSvgPathData()).Where(d => !string.IsNullOrWhiteSpace(d)).ToArray();

    Console.WriteLine(JsonSerializer.Serialize(comandosD));
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Falha ao traçar: {ex.Message}");
    return 1;
}

static string? LerOpcao(string[] args, string nome)
{
    var indice = Array.IndexOf(args, nome);
    return indice >= 0 && indice + 1 < args.Length ? args[indice + 1] : null;
}

static double LerOpcaoDouble(string[] args, string nome, double padrao)
{
    var texto = LerOpcao(args, nome);
    return texto is not null && double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor) ? valor : padrao;
}
