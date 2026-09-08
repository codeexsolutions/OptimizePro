using System.Text;

namespace OptimizePro.Core.Moldes.Dxf;

/// <summary>
/// Tokenizador de baixo nível do DXF: cada par código/valor ocupa duas linhas
/// (código, depois valor). Só ASCII — DXF binário é recusado (§8.1).
/// </summary>
public static class DxfLeitorDePares
{
    private const string AssinaturaBinaria = "AutoCAD Binary DXF";

    /// <summary>Retorna null se o arquivo for DXF binário (recusado pelo chamador).</summary>
    public static IReadOnlyList<DxfPar>? Ler(Stream conteudo)
    {
        using var leitor = new StreamReader(conteudo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        var linhaCodigo = leitor.ReadLine();
        if (linhaCodigo is null)
            return [];

        if (linhaCodigo.StartsWith(AssinaturaBinaria, StringComparison.Ordinal))
            return null;

        var pares = new List<DxfPar>();

        while (linhaCodigo is not null)
        {
            var linhaValor = leitor.ReadLine();
            if (linhaValor is null)
                break;

            if (int.TryParse(linhaCodigo.Trim(), out var codigo))
                pares.Add(new DxfPar(codigo, linhaValor.Trim()));

            linhaCodigo = leitor.ReadLine();
        }

        return pares;
    }
}
