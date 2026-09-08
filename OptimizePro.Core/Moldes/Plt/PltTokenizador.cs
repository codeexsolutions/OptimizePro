using System.Globalization;

namespace OptimizePro.Core.Moldes.Plt;

/// <summary>
/// Tokenizador de HP-GL (§8.1): mnemônico de 2 letras, terminado por <c>;</c> para a
/// maioria dos comandos; <c>LB</c> consome até um terminador (ETX por padrão, ou o
/// definido por <c>DT</c>); <c>PE</c> consome até <c>;</c> (dados ainda codificados,
/// decodificados depois por <see cref="PltDecodificadorPe"/>).
/// </summary>
/// <remarks>
/// Assume que comandos comuns sempre terminam em <c>;</c> — arquivos que dependem só da
/// adjacência do próximo mnemônico (sem <c>;</c>) não são suportados; é a forma mais
/// comum de HP-GL gerado por software de CAD/corte, mas vale validar contra arquivos reais.
/// </remarks>
public static class PltTokenizador
{
    public static IReadOnlyList<PltComando> Tokenizar(string conteudo)
    {
        var comandos = new List<PltComando>();
        var i = 0;
        var n = conteudo.Length;
        var terminadorDeTexto = '\x03'; // ETX — padrão do LB até DT redefinir

        while (i < n)
        {
            while (i < n && (conteudo[i] == ';' || char.IsWhiteSpace(conteudo[i])))
                i++;

            if (i + 1 >= n)
                break;

            var opcode = conteudo.Substring(i, 2).ToUpperInvariant();
            i += 2;

            if (opcode == "DT")
            {
                if (i < n)
                {
                    terminadorDeTexto = conteudo[i];
                    i++;
                }
                while (i < n && conteudo[i] != ';')
                    i++;
                if (i < n) i++;
                continue;
            }

            if (opcode == "LB")
            {
                var inicio = i;
                while (i < n && conteudo[i] != terminadorDeTexto)
                    i++;
                var texto = conteudo[inicio..i];
                if (i < n) i++; // consome o terminador

                comandos.Add(new PltComando(opcode, texto, []));
                continue;
            }

            if (opcode == "PE")
            {
                var inicio = i;
                while (i < n && conteudo[i] != ';')
                    i++;
                var dados = conteudo[inicio..i];
                if (i < n) i++;

                comandos.Add(new PltComando(opcode, dados, []));
                continue;
            }

            {
                var inicio = i;
                while (i < n && conteudo[i] != ';')
                    i++;
                var parametros = conteudo[inicio..i];
                if (i < n) i++;

                var numeros = parametros.Length == 0
                    ? []
                    : parametros
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(ParseDoubleInvariante)
                        .ToList();

                comandos.Add(new PltComando(opcode, null, numeros));
            }
        }

        return comandos;
    }

    private static double ParseDoubleInvariante(string valor) =>
        double.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
}
