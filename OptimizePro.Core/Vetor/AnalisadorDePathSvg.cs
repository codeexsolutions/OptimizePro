using System.Globalization;

namespace OptimizePro.Core.Vetor;

/// <summary>
/// Caminho inverso de <see cref="ConversorSvg.ParaComandoDePath"/> — interpreta um atributo
/// <c>d</c> de SVG de volta pra uma lista de <see cref="CaminhoMontado"/> (um por
/// subcaminho <c>M...Z</c>). Usado pra exportação (PDF, §20), não pelo pipeline de
/// vetorização em si.
/// </summary>
/// <remarks>
/// Suporta só comandos ABSOLUTOS (M, L, C, Q, A, Z) — os dois geradores de <c>d</c> deste
/// projeto (<see cref="ConversorSvg"/>, e <c>SKPath.ToSvgPathData()</c> do Potrace via
/// processo externo) só emitem absoluto; comando relativo (minúsculo) ou atalho (H/V/S/T)
/// lança <see cref="FormatException"/> em vez de produzir geometria errada silenciosamente.
/// Arco ('A') assume rx≈ry (raio único, igual <see cref="SegmentoArco"/>) — os dois
/// geradores também só emitem arco circular; suporte a elipse de verdade (rx≠ry) fica pra
/// quando for preciso.
/// </remarks>
public static class AnalisadorDePathSvg
{
    public static IReadOnlyList<CaminhoMontado> Analisar(string d)
    {
        var comandos = Tokenizar(d);
        var resultado = new List<CaminhoMontado>();

        var inicio = default(PontoXY);
        var atual = default(PontoXY);
        List<SegmentoDeCaminho>? segmentosAtuais = null;

        void FecharSubcaminhoAtual()
        {
            if (segmentosAtuais is not null)
                resultado.Add(new CaminhoMontado(inicio, segmentosAtuais));
        }

        foreach (var (cmd, args) in comandos)
        {
            switch (cmd)
            {
                case 'M':
                    FecharSubcaminhoAtual();
                    inicio = new PontoXY(args[0], args[1]);
                    atual = inicio;
                    segmentosAtuais = [];
                    break;

                case 'L':
                {
                    var fim = new PontoXY(args[0], args[1]);
                    segmentosAtuais!.Add(new SegmentoReta(fim));
                    atual = fim;
                    break;
                }

                case 'C':
                {
                    var fim = new PontoXY(args[4], args[5]);
                    segmentosAtuais!.Add(new SegmentoBezier(new PontoXY(args[0], args[1]), new PontoXY(args[2], args[3]), fim));
                    atual = fim;
                    break;
                }

                case 'Q':
                {
                    // Eleva quadrática (1 ponto de controle) pra cúbica (2), fórmula padrão.
                    var controle = new PontoXY(args[0], args[1]);
                    var fim = new PontoXY(args[2], args[3]);
                    var c1 = new PontoXY(atual.X + 2.0 / 3.0 * (controle.X - atual.X), atual.Y + 2.0 / 3.0 * (controle.Y - atual.Y));
                    var c2 = new PontoXY(fim.X + 2.0 / 3.0 * (controle.X - fim.X), fim.Y + 2.0 / 3.0 * (controle.Y - fim.Y));
                    segmentosAtuais!.Add(new SegmentoBezier(c1, c2, fim));
                    atual = fim;
                    break;
                }

                case 'A':
                {
                    var raio = (args[0] + args[1]) / 2.0;
                    var grandeArco = args[3] != 0;
                    var horario = args[4] != 0;
                    var fim = new PontoXY(args[5], args[6]);
                    segmentosAtuais!.Add(new SegmentoArco(raio, grandeArco, horario, fim));
                    atual = fim;
                    break;
                }

                case 'Z':
                    atual = inicio; // CaminhoMontado já implica fechado — nada a adicionar.
                    break;
            }
        }

        FecharSubcaminhoAtual();
        return resultado;
    }

    private static List<(char Cmd, double[] Args)> Tokenizar(string d)
    {
        var resultado = new List<(char, double[])>();
        var i = 0;
        var n = d.Length;

        static int ArgsPorComando(char c) => c switch
        {
            'M' or 'L' => 2,
            'Q' => 4,
            'C' => 6,
            'A' => 7,
            'Z' => 0,
            _ => throw new FormatException($"Comando de path SVG não suportado: '{c}' (só M/L/C/Q/A/Z absolutos são suportados)."),
        };

        void PularSeparadores()
        {
            while (i < n && (char.IsWhiteSpace(d[i]) || d[i] == ',')) i++;
        }

        bool ProximoPareceNumero() => i < n && (char.IsAsciiDigit(d[i]) || d[i] == '-' || d[i] == '+' || d[i] == '.');

        double LerNumero()
        {
            PularSeparadores();
            var inicioNumero = i;
            if (i < n && (d[i] == '+' || d[i] == '-')) i++;
            while (i < n && char.IsAsciiDigit(d[i])) i++;
            if (i < n && d[i] == '.') { i++; while (i < n && char.IsAsciiDigit(d[i])) i++; }
            if (i < n && (d[i] == 'e' || d[i] == 'E'))
            {
                i++;
                if (i < n && (d[i] == '+' || d[i] == '-')) i++;
                while (i < n && char.IsAsciiDigit(d[i])) i++;
            }
            if (i == inicioNumero)
                throw new FormatException($"Número inválido no path SVG (posição {i}): '{d}'");

            return double.Parse(d.AsSpan(inicioNumero, i - inicioNumero), CultureInfo.InvariantCulture);
        }

        while (true)
        {
            PularSeparadores();
            if (i >= n) break;

            var cmd = d[i];
            i++;
            var numArgs = ArgsPorComando(cmd);

            if (numArgs == 0)
            {
                resultado.Add((cmd, []));
                continue;
            }

            while (true)
            {
                var args = new double[numArgs];
                for (var k = 0; k < numArgs; k++) args[k] = LerNumero();
                resultado.Add((cmd, args));

                PularSeparadores();
                if (!ProximoPareceNumero())
                    break; // próximo é uma letra de comando (ou fim do texto) — sem repetição implícita
            }
        }

        return resultado;
    }
}
