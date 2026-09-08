using System.Globalization;

namespace OptimizePro.Core.Moldes.Svg;

/// <summary>Tokenizador de baixo nível do atributo <c>d</c> — números podem vir colados sem separador.</summary>
internal sealed class SvgTokenizadorDePath(string dados)
{
    private int _i;

    private void PularSeparadores()
    {
        while (_i < dados.Length && (char.IsWhiteSpace(dados[_i]) || dados[_i] == ','))
            _i++;
    }

    /// <summary>Devolve o próximo comando (letra) sem consumir números; null se o próximo token não for uma letra.</summary>
    public char? ProximoComando()
    {
        PularSeparadores();
        if (_i >= dados.Length)
            return null;

        var c = dados[_i];
        if (!char.IsLetter(c))
            return null;

        _i++;
        return c;
    }

    public bool TemNumero()
    {
        var salvo = _i;
        var ok = TentarLerNumero(out _);
        _i = salvo;
        return ok;
    }

    public double LerNumero()
    {
        if (!TentarLerNumero(out var valor))
            throw new FormatException($"Número inválido em path SVG na posição {_i}.");
        return valor;
    }

    /// <summary>Lê um único dígito de flag (0 ou 1) do comando <c>A</c> — pode vir colado ao número seguinte.</summary>
    public int LerFlag()
    {
        PularSeparadores();
        if (_i >= dados.Length)
            throw new FormatException("Flag de arco ausente em path SVG.");

        var c = dados[_i];
        _i++;
        return c == '1' ? 1 : 0;
    }

    private bool TentarLerNumero(out double valor)
    {
        PularSeparadores();
        var inicio = _i;
        var n = dados.Length;

        if (_i < n && (dados[_i] == '+' || dados[_i] == '-'))
            _i++;

        var teveDigitosAntes = false;
        while (_i < n && char.IsAsciiDigit(dados[_i])) { _i++; teveDigitosAntes = true; }

        var teveDigitosDepois = false;
        if (_i < n && dados[_i] == '.')
        {
            _i++;
            while (_i < n && char.IsAsciiDigit(dados[_i])) { _i++; teveDigitosDepois = true; }
        }

        if (!teveDigitosAntes && !teveDigitosDepois)
        {
            _i = inicio;
            valor = 0;
            return false;
        }

        if (_i < n && (dados[_i] == 'e' || dados[_i] == 'E'))
        {
            var voltaExpo = _i;
            _i++;
            if (_i < n && (dados[_i] == '+' || dados[_i] == '-'))
                _i++;

            var teveDigitosExpo = false;
            while (_i < n && char.IsAsciiDigit(dados[_i])) { _i++; teveDigitosExpo = true; }

            if (!teveDigitosExpo)
                _i = voltaExpo;
        }

        valor = double.Parse(dados.AsSpan(inicio, _i - inicio), NumberStyles.Float, CultureInfo.InvariantCulture);
        return true;
    }
}
