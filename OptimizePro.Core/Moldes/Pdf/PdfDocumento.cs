namespace OptimizePro.Core.Moldes.Pdf;

public sealed class PdfDocumento
{
    /// <summary>Número do objeto → (dicionário, bytes crus do stream, se houver).</summary>
    public Dictionary<int, (Dictionary<string, object?> Dicionario, byte[]? Stream)> Objetos { get; } = [];

    public static Dictionary<string, object?>? ResolverParaDicionario(PdfDocumento doc, object? valor) => valor switch
    {
        PdfReferencia r when doc.Objetos.TryGetValue(r.Numero, out var obj) => obj.Dicionario,
        Dictionary<string, object?> d => d,
        _ => null,
    };

    public static (Dictionary<string, object?> Dicionario, byte[]? Stream)? ResolverObjetoIndireto(PdfDocumento doc, object? valor) => valor switch
    {
        PdfReferencia r when doc.Objetos.TryGetValue(r.Numero, out var obj) => obj,
        Dictionary<string, object?> d => (d, null),
        _ => null,
    };

    public static string? NomeDoTipo(Dictionary<string, object?>? dict) =>
        dict is not null && dict.TryGetValue("Type", out var v) && v is string s ? s : null;
}
