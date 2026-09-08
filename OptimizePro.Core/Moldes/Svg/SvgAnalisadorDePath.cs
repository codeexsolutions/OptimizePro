namespace OptimizePro.Core.Moldes.Svg;

/// <summary>
/// Parser do atributo <c>d</c> do <c>&lt;path&gt;</c> (§8.1: "será necessário implementar
/// path-flattening manualmente"). Cada <c>M</c>/<c>m</c> inicia um novo subcaminho — isso
/// já delimita furos/peças soltas com precisão, sem precisar do heurístico de "salto de
/// amostragem" mencionado na especificação (que existia para compensar a amostragem
/// uniforme do <c>getPointAtLength</c> do browser; aqui a gramática do path já dá os
/// limites exatos de cada subcaminho).
/// </summary>
public static class SvgAnalisadorDePath
{
    public static IReadOnlyList<Traco> Analisar(string d, List<string> avisos)
    {
        var subcaminhos = new List<Traco>();
        var pontosAtuais = new List<PontoXY>();

        var tok = new SvgTokenizadorDePath(d);
        var posicaoAtual = new PontoXY(0, 0);
        var inicioDoSubcaminho = new PontoXY(0, 0);
        char? comandoAtual = null;
        PontoXY? ultimoControleCubica = null;
        PontoXY? ultimoControleQuadratica = null;

        void FinalizarSubcaminho(bool fechado)
        {
            if (pontosAtuais.Count >= 2)
                subcaminhos.Add(new Traco([.. pontosAtuais], fechado));
            pontosAtuais = [];
        }

        while (true)
        {
            var novoComando = tok.ProximoComando();
            if (novoComando is not null)
                comandoAtual = novoComando;
            else if (comandoAtual is null || !tok.TemNumero())
                break;

            var c = comandoAtual!.Value;
            var absoluto = char.IsUpper(c);
            var cUpper = char.ToUpperInvariant(c);

            if (cUpper == 'Z')
            {
                if (pontosAtuais.Count > 0)
                    pontosAtuais.Add(inicioDoSubcaminho);
                FinalizarSubcaminho(true);
                posicaoAtual = inicioDoSubcaminho;
                comandoAtual = null;
                ultimoControleCubica = null;
                ultimoControleQuadratica = null;
                continue;
            }

            if (!tok.TemNumero())
                break;

            switch (cUpper)
            {
                case 'M':
                {
                    var x = tok.LerNumero();
                    var y = tok.LerNumero();
                    var ponto = absoluto ? new PontoXY(x, y) : new PontoXY(posicaoAtual.X + x, posicaoAtual.Y + y);
                    FinalizarSubcaminho(false);
                    pontosAtuais.Add(ponto);
                    posicaoAtual = ponto;
                    inicioDoSubcaminho = ponto;
                    comandoAtual = absoluto ? 'L' : 'l'; // pares seguintes = lineto implícito
                    ultimoControleCubica = null;
                    ultimoControleQuadratica = null;
                    break;
                }

                case 'L':
                {
                    var x = tok.LerNumero();
                    var y = tok.LerNumero();
                    var ponto = absoluto ? new PontoXY(x, y) : new PontoXY(posicaoAtual.X + x, posicaoAtual.Y + y);
                    pontosAtuais.Add(ponto);
                    posicaoAtual = ponto;
                    ultimoControleCubica = null;
                    ultimoControleQuadratica = null;
                    break;
                }

                case 'H':
                {
                    var x = tok.LerNumero();
                    var ponto = new PontoXY(absoluto ? x : posicaoAtual.X + x, posicaoAtual.Y);
                    pontosAtuais.Add(ponto);
                    posicaoAtual = ponto;
                    ultimoControleCubica = null;
                    ultimoControleQuadratica = null;
                    break;
                }

                case 'V':
                {
                    var y = tok.LerNumero();
                    var ponto = new PontoXY(posicaoAtual.X, absoluto ? y : posicaoAtual.Y + y);
                    pontosAtuais.Add(ponto);
                    posicaoAtual = ponto;
                    ultimoControleCubica = null;
                    ultimoControleQuadratica = null;
                    break;
                }

                case 'C':
                {
                    var x1 = tok.LerNumero(); var y1 = tok.LerNumero();
                    var x2 = tok.LerNumero(); var y2 = tok.LerNumero();
                    var x = tok.LerNumero(); var y = tok.LerNumero();
                    var p1 = absoluto ? new PontoXY(x1, y1) : new PontoXY(posicaoAtual.X + x1, posicaoAtual.Y + y1);
                    var p2 = absoluto ? new PontoXY(x2, y2) : new PontoXY(posicaoAtual.X + x2, posicaoAtual.Y + y2);
                    var pFinal = absoluto ? new PontoXY(x, y) : new PontoXY(posicaoAtual.X + x, posicaoAtual.Y + y);
                    pontosAtuais.AddRange(SvgCurvas.AmostrarCubica(posicaoAtual, p1, p2, pFinal));
                    posicaoAtual = pFinal;
                    ultimoControleCubica = p2;
                    ultimoControleQuadratica = null;
                    break;
                }

                case 'S':
                {
                    var x2 = tok.LerNumero(); var y2 = tok.LerNumero();
                    var x = tok.LerNumero(); var y = tok.LerNumero();
                    var p1 = ultimoControleCubica is { } uc
                        ? new PontoXY(2 * posicaoAtual.X - uc.X, 2 * posicaoAtual.Y - uc.Y)
                        : posicaoAtual;
                    var p2 = absoluto ? new PontoXY(x2, y2) : new PontoXY(posicaoAtual.X + x2, posicaoAtual.Y + y2);
                    var pFinal = absoluto ? new PontoXY(x, y) : new PontoXY(posicaoAtual.X + x, posicaoAtual.Y + y);
                    pontosAtuais.AddRange(SvgCurvas.AmostrarCubica(posicaoAtual, p1, p2, pFinal));
                    posicaoAtual = pFinal;
                    ultimoControleCubica = p2;
                    ultimoControleQuadratica = null;
                    break;
                }

                case 'Q':
                {
                    var x1 = tok.LerNumero(); var y1 = tok.LerNumero();
                    var x = tok.LerNumero(); var y = tok.LerNumero();
                    var p1 = absoluto ? new PontoXY(x1, y1) : new PontoXY(posicaoAtual.X + x1, posicaoAtual.Y + y1);
                    var pFinal = absoluto ? new PontoXY(x, y) : new PontoXY(posicaoAtual.X + x, posicaoAtual.Y + y);
                    pontosAtuais.AddRange(SvgCurvas.AmostrarQuadratica(posicaoAtual, p1, pFinal));
                    posicaoAtual = pFinal;
                    ultimoControleQuadratica = p1;
                    ultimoControleCubica = null;
                    break;
                }

                case 'T':
                {
                    var x = tok.LerNumero(); var y = tok.LerNumero();
                    var p1 = ultimoControleQuadratica is { } uq
                        ? new PontoXY(2 * posicaoAtual.X - uq.X, 2 * posicaoAtual.Y - uq.Y)
                        : posicaoAtual;
                    var pFinal = absoluto ? new PontoXY(x, y) : new PontoXY(posicaoAtual.X + x, posicaoAtual.Y + y);
                    pontosAtuais.AddRange(SvgCurvas.AmostrarQuadratica(posicaoAtual, p1, pFinal));
                    posicaoAtual = pFinal;
                    ultimoControleQuadratica = p1;
                    ultimoControleCubica = null;
                    break;
                }

                case 'A':
                {
                    var rx = tok.LerNumero(); var ry = tok.LerNumero();
                    var xrot = tok.LerNumero();
                    var largeArc = tok.LerFlag();
                    var sweep = tok.LerFlag();
                    var x = tok.LerNumero(); var y = tok.LerNumero();
                    var pFinal = absoluto ? new PontoXY(x, y) : new PontoXY(posicaoAtual.X + x, posicaoAtual.Y + y);
                    pontosAtuais.AddRange(SvgCurvas.AmostrarArcoEliptico(posicaoAtual, pFinal, rx, ry, xrot, largeArc, sweep));
                    posicaoAtual = pFinal;
                    ultimoControleCubica = null;
                    ultimoControleQuadratica = null;
                    break;
                }

                default:
                    avisos.Add($"Comando de path SVG não suportado: '{c}' — resto do path ignorado.");
                    FinalizarSubcaminho(false);
                    return subcaminhos;
            }
        }

        FinalizarSubcaminho(false);
        return subcaminhos;
    }
}
