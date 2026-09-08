using System.Text.RegularExpressions;

namespace OptimizePro.Services.Encaixe;

public sealed record QuantidadeDoNome(string Nome, int Quantidade, bool VeioDoNome);

/// <summary>
/// Porte de <c>lerQuantidadeDoNome</c> (`encaixe.js`, projeto original) — muita arte já chega
/// com a quantidade no próprio nome do arquivo ("frente 5x.png", "x3 manga.png",
/// "costas-12x.png"). O cuidado é não confundir quantidade com medida: "camisa 30x40.png" é
/// tamanho, não 30 peças — por isso o "x" da quantidade não pode ter número dos dois lados.
/// </summary>
public static partial class LeitorDeQuantidadeDoNome
{
    [GeneratedRegex(@"\d+\s*[xX]\s*\d+")]
    private static partial Regex PadraoDeMedida();

    // "5x", "12 x", "costas-8x", "manga4x".
    [GeneratedRegex(@"(^|[^\d])(\d{1,4})\s*[xX](?=$|[\s._\-)\]])")]
    private static partial Regex PadraoQtdSufixo();

    // "x5", "x 12".
    [GeneratedRegex(@"(^|[^\d\0])[xX]\s*(\d{1,4})(?=$|[\s._\-)\]])")]
    private static partial Regex PadraoQtdPrefixo();

    [GeneratedRegex(@"\(\s*\)|\[\s*\]")]
    private static partial Regex ParentesesVazios();

    [GeneratedRegex(@"[\s._\-]{2,}")]
    private static partial Regex SeparadoresRepetidos();

    [GeneratedRegex(@"^[\s._\-]+|[\s._\-]+$")]
    private static partial Regex SeparadoresNasBordas();

    [GeneratedRegex(@"[a-zA-ZÀ-ÿ]")]
    private static partial Regex TemPalavra();

    public static QuantidadeDoNome Ler(string nomeArquivo)
    {
        // Mascara as medidas ("30x40", "30 x 40") antes: elas têm número dos dois lados do x e
        // não são quantidade. \0 ocupa o mesmo tanto de caracteres pra posições continuarem valendo.
        var semMedidas = PadraoDeMedida().Replace(nomeArquivo, m => new string('\0', m.Length));

        foreach (var padrao in new[] { PadraoQtdSufixo(), PadraoQtdPrefixo() })
        {
            var achado = padrao.Match(semMedidas);
            if (!achado.Success) continue;

            var qtd = int.Parse(achado.Groups[2].Value);
            if (qtd < 1) continue;

            var inicio = achado.Index + achado.Groups[1].Length;
            var nome = (nomeArquivo[..inicio] + nomeArquivo[(achado.Index + achado.Length)..]);
            nome = ParentesesVazios().Replace(nome, "");
            nome = SeparadoresRepetidos().Replace(nome, " ");
            nome = SeparadoresNasBordas().Replace(nome, "").Trim();

            // "5x (1).jpg" deixaria a peça chamada "(1)" — quando só sobra pontuação/número de
            // cópia, o nome do arquivo inteiro informa mais.
            return new QuantidadeDoNome(TemPalavra().IsMatch(nome) ? nome : nomeArquivo, qtd, true);
        }

        return new QuantidadeDoNome(nomeArquivo, 1, false);
    }
}
