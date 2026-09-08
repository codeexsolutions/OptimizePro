using System.Text;

namespace OptimizePro.Licenciamento;

/// <summary>
/// Base32 na variante de Douglas Crockford (https://www.crockford.com/base32.html) — usada
/// pro código de licença porque o alfabeto evita letras/números parecidos (sem I, L, O, U),
/// então um cliente copiando/colando ou digitando o código à mão erra menos.
/// </summary>
public static class Base32Crockford
{
    private const string Alfabeto = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Codificar(ReadOnlySpan<byte> dados)
    {
        var saida = new StringBuilder((dados.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsNoBuffer = 0;

        foreach (var b in dados)
        {
            buffer = (buffer << 8) | b;
            bitsNoBuffer += 8;

            while (bitsNoBuffer >= 5)
            {
                bitsNoBuffer -= 5;
                saida.Append(Alfabeto[(buffer >> bitsNoBuffer) & 0x1F]);
            }
        }

        if (bitsNoBuffer > 0)
            saida.Append(Alfabeto[(buffer << (5 - bitsNoBuffer)) & 0x1F]);

        return saida.ToString();
    }

    /// <summary>Devolve <c>null</c> (em vez de lançar) pra código com caractere fora do alfabeto — chamador trata como "código inválido", não crash.</summary>
    public static byte[]? Decodificar(string texto)
    {
        var saida = new List<byte>((texto.Length * 5 + 7) / 8);
        var buffer = 0;
        var bitsNoBuffer = 0;

        foreach (var c in texto)
        {
            if (c is '-' or ' ') continue; // separadores visuais são ignorados na decodificação.

            var valor = Alfabeto.IndexOf(char.ToUpperInvariant(c));
            if (valor < 0) return null;

            buffer = (buffer << 5) | valor;
            bitsNoBuffer += 5;

            if (bitsNoBuffer >= 8)
            {
                bitsNoBuffer -= 8;
                saida.Add((byte)((buffer >> bitsNoBuffer) & 0xFF));
            }
        }

        return [.. saida];
    }

    /// <summary>Insere um traço a cada <paramref name="tamanhoDoGrupo"/> caracteres — só cosmético, pra ficar mais fácil de ler/copiar.</summary>
    public static string ComTracos(string codigo, int tamanhoDoGrupo = 5)
    {
        var saida = new StringBuilder(codigo.Length + codigo.Length / tamanhoDoGrupo);
        for (var i = 0; i < codigo.Length; i++)
        {
            if (i > 0 && i % tamanhoDoGrupo == 0) saida.Append('-');
            saida.Append(codigo[i]);
        }
        return saida.ToString();
    }
}
