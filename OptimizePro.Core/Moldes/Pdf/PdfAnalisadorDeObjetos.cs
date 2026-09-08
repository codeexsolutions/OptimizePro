using System.Globalization;
using System.Text;

namespace OptimizePro.Core.Moldes.Pdf;

/// <summary>
/// Parser artesanal de objetos PDF (dicionários, arrays, nomes, strings, números,
/// referências) — opera sobre o arquivo inteiro decodificado como Latin-1 (1 byte =
/// 1 char, sem perdas), o mesmo texto sendo reconvertido para bytes quando necessário
/// (streams). Usado tanto para os objetos indiretos do documento quanto para os
/// operandos do interpretador de content stream (§8.1).
/// </summary>
/// <remarks>
/// Representação em C#: dicionário → <see cref="Dictionary{TKey,TValue}"/>; array →
/// <see cref="List{T}"/>; nome e string → <see cref="string"/> (ambos decodificados para
/// texto; a distinção nome/string não importa para o pipeline de geometria, só para
/// decisões pontuais como <c>/Filter</c>, feitas por quem já sabe qual esperar); número →
/// <see cref="double"/>; referência → <see cref="PdfReferencia"/>; booleano →
/// <see cref="bool"/>; nulo → <see langword="null"/>.
/// </remarks>
public static class PdfAnalisadorDeObjetos
{
    public static object? AnalisarValor(string texto, ref int pos)
    {
        PularEspacosEComentarios(texto, ref pos);
        if (pos >= texto.Length)
            return null;

        var c = texto[pos];

        if (c == '<' && pos + 1 < texto.Length && texto[pos + 1] == '<')
            return AnalisarDicionario(texto, ref pos);

        if (c == '<')
            return AnalisarStringHex(texto, ref pos);

        if (c == '[')
            return AnalisarArray(texto, ref pos);

        if (c == '/')
            return AnalisarNome(texto, ref pos);

        if (c == '(')
            return AnalisarStringLiteral(texto, ref pos);

        if (c == '+' || c == '-' || c == '.' || char.IsAsciiDigit(c))
            return AnalisarNumeroOuReferencia(texto, ref pos);

        if (Corresponde(texto, pos, "true")) { pos += 4; return true; }
        if (Corresponde(texto, pos, "false")) { pos += 5; return false; }
        if (Corresponde(texto, pos, "null")) { pos += 4; return null; }

        // token inesperado (provavelmente um operador de content stream) — não consome,
        // deixa o chamador (interpretador de content stream) decidir o que fazer.
        return SentinelaDesconhecido.Instancia;
    }

    /// <summary>Marca "não é um objeto PDF válido aqui" sem avançar o cursor — usado pelo interpretador de content stream para reconhecer operadores.</summary>
    public sealed class SentinelaDesconhecido
    {
        public static readonly SentinelaDesconhecido Instancia = new();
        private SentinelaDesconhecido() { }
    }

    private static bool Corresponde(string texto, int pos, string literal) =>
        pos + literal.Length <= texto.Length && string.CompareOrdinal(texto, pos, literal, 0, literal.Length) == 0;

    private static Dictionary<string, object?> AnalisarDicionario(string texto, ref int pos)
    {
        pos += 2; // "<<"
        var dict = new Dictionary<string, object?>();

        while (true)
        {
            PularEspacosEComentarios(texto, ref pos);
            if (pos + 1 < texto.Length && texto[pos] == '>' && texto[pos + 1] == '>') { pos += 2; break; }
            if (pos >= texto.Length) break;

            if (texto[pos] != '/') { pos++; continue; } // recuperação simples de erro

            var chave = AnalisarNome(texto, ref pos);
            var valor = AnalisarValor(texto, ref pos);
            dict[chave] = valor is SentinelaDesconhecido ? null : valor;
        }

        return dict;
    }

    private static List<object?> AnalisarArray(string texto, ref int pos)
    {
        pos++; // '['
        var lista = new List<object?>();

        while (true)
        {
            PularEspacosEComentarios(texto, ref pos);
            if (pos >= texto.Length) break;
            if (texto[pos] == ']') { pos++; break; }

            var valor = AnalisarValor(texto, ref pos);
            if (valor is SentinelaDesconhecido) { pos++; continue; } // token estranho — recupera avançando
            lista.Add(valor);
        }

        return lista;
    }

    internal static string AnalisarNome(string texto, ref int pos)
    {
        pos++; // '/'
        var sb = new StringBuilder();

        while (pos < texto.Length && !EhDelimitadorOuEspaco(texto[pos]))
        {
            if (texto[pos] == '#' && pos + 2 < texto.Length && Uri.IsHexDigit(texto[pos + 1]) && Uri.IsHexDigit(texto[pos + 2]))
            {
                sb.Append((char)Convert.ToInt32(texto.Substring(pos + 1, 2), 16));
                pos += 3;
            }
            else
            {
                sb.Append(texto[pos]);
                pos++;
            }
        }

        return sb.ToString();
    }

    private static string AnalisarStringLiteral(string texto, ref int pos)
    {
        pos++; // '('
        var sb = new StringBuilder();
        var profundidade = 1;

        while (pos < texto.Length && profundidade > 0)
        {
            var c = texto[pos];

            if (c == '\\')
            {
                pos++;
                if (pos >= texto.Length) break;
                var e = texto[pos];

                switch (e)
                {
                    case 'n': sb.Append('\n'); pos++; break;
                    case 'r': sb.Append('\r'); pos++; break;
                    case 't': sb.Append('\t'); pos++; break;
                    case 'b': sb.Append('\b'); pos++; break;
                    case 'f': sb.Append('\f'); pos++; break;
                    case '(': sb.Append('('); pos++; break;
                    case ')': sb.Append(')'); pos++; break;
                    case '\\': sb.Append('\\'); pos++; break;
                    case '\r':
                        pos++;
                        if (pos < texto.Length && texto[pos] == '\n') pos++;
                        break;
                    case '\n':
                        pos++;
                        break;
                    default:
                        if (char.IsAsciiDigit(e) && e < '8')
                        {
                            var octal = 0;
                            var digitos = 0;
                            while (digitos < 3 && pos < texto.Length && texto[pos] >= '0' && texto[pos] <= '7')
                            {
                                octal = octal * 8 + (texto[pos] - '0');
                                pos++;
                                digitos++;
                            }
                            sb.Append((char)(octal & 0xFF));
                        }
                        else
                        {
                            sb.Append(e);
                            pos++;
                        }
                        break;
                }
                continue;
            }

            if (c == '(') { profundidade++; sb.Append(c); pos++; continue; }
            if (c == ')')
            {
                profundidade--;
                pos++;
                if (profundidade > 0) sb.Append(c);
                continue;
            }

            sb.Append(c);
            pos++;
        }

        return sb.ToString();
    }

    private static string AnalisarStringHex(string texto, ref int pos)
    {
        pos++; // '<'
        var digitos = new List<char>();

        while (pos < texto.Length && texto[pos] != '>')
        {
            if (Uri.IsHexDigit(texto[pos]))
                digitos.Add(texto[pos]);
            pos++;
        }
        if (pos < texto.Length) pos++; // '>'

        if (digitos.Count % 2 != 0)
            digitos.Add('0');

        var sb = new StringBuilder(digitos.Count / 2);
        for (var i = 0; i + 1 < digitos.Count; i += 2)
            sb.Append((char)Convert.ToInt32($"{digitos[i]}{digitos[i + 1]}", 16));

        return sb.ToString();
    }

    private static object AnalisarNumeroOuReferencia(string texto, ref int pos)
    {
        var primeiro = LerNumero(texto, ref pos);

        if (primeiro >= 0 && primeiro == Math.Floor(primeiro))
        {
            var salvo = pos;
            PularEspacos(texto, ref pos);

            if (pos < texto.Length && char.IsAsciiDigit(texto[pos]))
            {
                var segundo = LerNumero(texto, ref pos);
                PularEspacos(texto, ref pos);

                if (pos < texto.Length && texto[pos] == 'R' && (pos + 1 >= texto.Length || EhDelimitadorOuEspaco(texto[pos + 1])))
                {
                    pos++; // 'R'
                    return new PdfReferencia((int)primeiro, (int)segundo);
                }
            }

            pos = salvo;
        }

        return primeiro;
    }

    internal static double LerNumero(string texto, ref int pos)
    {
        PularEspacosEComentarios(texto, ref pos);

        var inicio = pos;
        if (pos < texto.Length && (texto[pos] == '+' || texto[pos] == '-'))
            pos++;

        while (pos < texto.Length && char.IsAsciiDigit(texto[pos]))
            pos++;

        if (pos < texto.Length && texto[pos] == '.')
        {
            pos++;
            while (pos < texto.Length && char.IsAsciiDigit(texto[pos]))
                pos++;
        }

        var span = texto.AsSpan(inicio, pos - inicio);
        return double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    internal static void PularEspacosEComentarios(string texto, ref int pos)
    {
        while (pos < texto.Length)
        {
            if (char.IsWhiteSpace(texto[pos])) { pos++; continue; }
            if (texto[pos] == '%')
            {
                while (pos < texto.Length && texto[pos] != '\n' && texto[pos] != '\r') pos++;
                continue;
            }
            break;
        }
    }

    private static void PularEspacos(string texto, ref int pos)
    {
        while (pos < texto.Length && char.IsWhiteSpace(texto[pos])) pos++;
    }

    private static bool EhDelimitadorOuEspaco(char c) =>
        char.IsWhiteSpace(c) || c is '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%';
}
