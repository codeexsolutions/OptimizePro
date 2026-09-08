using System.Text;
using OptimizePro.Core.Moldes.Svg;

namespace OptimizePro.Core.Moldes.Pdf;

/// <summary>
/// Interpretador de content stream PDF (§8.1): <c>q/Q/cm</c> (pilha de matriz),
/// <c>m/l/c/v/y/h/re</c> (path), <c>n/f/F/f*/S/s/B/B*/b/b*</c> (pintar), <c>Do</c>
/// (XObject de formulário, recursivo até profundidade 6), operadores de texto (só
/// posição/conteúdo — sem geometria de glifo, igual à especificação original).
/// </summary>
public static class PdfConteudoInterpretador
{
    private const int ProfundidadeMaximaDeForm = 6;

    public static void Interpretar(
        string conteudo, Transformacao2D ctmInicial, Dictionary<string, object?>? recursos, int profundidade,
        PdfDocumento doc, List<Traco> tracos, List<RotuloTexto> textos, List<string> avisos)
    {
        if (profundidade > ProfundidadeMaximaDeForm)
        {
            avisos.Add($"Profundidade máxima de XObject Form ({ProfundidadeMaximaDeForm}) excedida — ignorado.");
            return;
        }

        var pos = 0;
        var pilhaOperandos = new List<object?>();
        var pilhaCtm = new Stack<Transformacao2D>();
        var ctm = ctmInicial;

        var subcaminhoAtual = new List<PontoXY>();
        var subcaminhosFechados = new List<(List<PontoXY> Pontos, bool Fechado)>();
        var posicaoCorrente = new PontoXY(0, 0);
        var inicioDoSubcaminho = new PontoXY(0, 0);

        var matrizDeTexto = Transformacao2D.Identidade;
        var matrizDeLinhaDeTexto = Transformacao2D.Identidade;

        void FecharSubcaminhoAberto()
        {
            if (subcaminhoAtual.Count >= 2)
                subcaminhosFechados.Add((subcaminhoAtual, false));
            subcaminhoAtual = [];
        }

        void FinalizarPath(bool forcarFechado)
        {
            FecharSubcaminhoAberto();
            foreach (var (pontos, fechadoExplicito) in subcaminhosFechados)
            {
                var pts = pontos;
                var fechado = fechadoExplicito || forcarFechado;
                if (fechado && pts.Count > 0 && pts[0] != pts[^1])
                    pts = [.. pts, pts[0]];

                tracos.Add(new Traco([.. pts.Select(ctm.Aplicar)], fechado));
            }
            subcaminhosFechados = [];
        }

        void DescartarPath()
        {
            subcaminhoAtual = [];
            subcaminhosFechados = [];
        }

        double[] Numeros(int n)
        {
            var todos = pilhaOperandos.OfType<double>().ToList();
            var resultado = new double[n];
            var inicio = Math.Max(0, todos.Count - n);
            for (var i = 0; i < n && inicio + i < todos.Count; i++)
                resultado[i] = todos[inicio + i];
            return resultado;
        }

        void RegistrarTexto(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return;

            var p = ctm.ComposicaoCom(matrizDeTexto).Aplicar(new PontoXY(0, 0));
            textos.Add(new RotuloTexto(texto, p.X, p.Y));
        }

        while (pos < conteudo.Length)
        {
            PdfAnalisadorDeObjetos.PularEspacosEComentarios(conteudo, ref pos);
            if (pos >= conteudo.Length) break;

            var c = conteudo[pos];

            if (c is '<' or '[' or '/' or '(' or '+' or '-' or '.' || char.IsAsciiDigit(c))
            {
                var valor = PdfAnalisadorDeObjetos.AnalisarValor(conteudo, ref pos);
                if (valor is not PdfAnalisadorDeObjetos.SentinelaDesconhecido)
                {
                    pilhaOperandos.Add(valor);
                    continue;
                }
            }

            var inicioOp = pos;
            while (pos < conteudo.Length && !char.IsWhiteSpace(conteudo[pos]) && conteudo[pos] is not ('<' or '[' or '/' or '(' or ')' or '>' or ']'))
                pos++;

            if (pos == inicioOp) { pos++; continue; } // caractere isolado inesperado — evita loop infinito

            var op = conteudo[inicioOp..pos];

            switch (op)
            {
                case "q": pilhaCtm.Push(ctm); break;
                case "Q": if (pilhaCtm.Count > 0) ctm = pilhaCtm.Pop(); break;
                case "cm":
                {
                    var v = Numeros(6);
                    ctm = ctm.ComposicaoCom(Transformacao2D.DeMatriz(v[0], v[1], v[2], v[3], v[4], v[5]));
                    break;
                }
                case "m":
                {
                    var v = Numeros(2);
                    FecharSubcaminhoAberto();
                    var p = new PontoXY(v[0], v[1]);
                    subcaminhoAtual.Add(p);
                    posicaoCorrente = p;
                    inicioDoSubcaminho = p;
                    break;
                }
                case "l":
                {
                    var v = Numeros(2);
                    if (subcaminhoAtual.Count == 0) subcaminhoAtual.Add(posicaoCorrente);
                    posicaoCorrente = new PontoXY(v[0], v[1]);
                    subcaminhoAtual.Add(posicaoCorrente);
                    break;
                }
                case "c":
                {
                    var v = Numeros(6);
                    if (subcaminhoAtual.Count == 0) subcaminhoAtual.Add(posicaoCorrente);
                    var p3 = new PontoXY(v[4], v[5]);
                    subcaminhoAtual.AddRange(SvgCurvas.AmostrarCubica(posicaoCorrente, new PontoXY(v[0], v[1]), new PontoXY(v[2], v[3]), p3));
                    posicaoCorrente = p3;
                    break;
                }
                case "v":
                {
                    var v = Numeros(4);
                    if (subcaminhoAtual.Count == 0) subcaminhoAtual.Add(posicaoCorrente);
                    var p3 = new PontoXY(v[2], v[3]);
                    subcaminhoAtual.AddRange(SvgCurvas.AmostrarCubica(posicaoCorrente, posicaoCorrente, new PontoXY(v[0], v[1]), p3));
                    posicaoCorrente = p3;
                    break;
                }
                case "y":
                {
                    var v = Numeros(4);
                    if (subcaminhoAtual.Count == 0) subcaminhoAtual.Add(posicaoCorrente);
                    var p3 = new PontoXY(v[2], v[3]);
                    subcaminhoAtual.AddRange(SvgCurvas.AmostrarCubica(posicaoCorrente, new PontoXY(v[0], v[1]), p3, p3));
                    posicaoCorrente = p3;
                    break;
                }
                case "h":
                    if (subcaminhoAtual.Count > 0)
                    {
                        subcaminhoAtual.Add(inicioDoSubcaminho);
                        subcaminhosFechados.Add((subcaminhoAtual, true));
                        subcaminhoAtual = [];
                        posicaoCorrente = inicioDoSubcaminho;
                    }
                    break;
                case "re":
                {
                    var v = Numeros(4);
                    FecharSubcaminhoAberto();
                    var x = v[0]; var y = v[1]; var w = v[2]; var h = v[3];
                    subcaminhosFechados.Add(([new(x, y), new(x + w, y), new(x + w, y + h), new(x, y + h), new(x, y)], true));
                    posicaoCorrente = new PontoXY(x, y);
                    inicioDoSubcaminho = posicaoCorrente;
                    break;
                }
                case "n": DescartarPath(); break;
                case "f": case "F": case "f*": FinalizarPath(true); break;
                case "S": FinalizarPath(false); break;
                case "s": FinalizarPath(true); break;
                case "B": case "B*": case "b": case "b*": FinalizarPath(true); break;

                case "BT":
                    matrizDeTexto = Transformacao2D.Identidade;
                    matrizDeLinhaDeTexto = Transformacao2D.Identidade;
                    break;
                case "ET": break;
                case "Td":
                case "TD":
                {
                    var v = Numeros(2);
                    matrizDeLinhaDeTexto = matrizDeLinhaDeTexto.ComposicaoCom(Transformacao2D.DeEscalaRotacaoTranslacao(1, 1, 0, v[0], v[1]));
                    matrizDeTexto = matrizDeLinhaDeTexto;
                    break;
                }
                case "Tm":
                {
                    var v = Numeros(6);
                    matrizDeLinhaDeTexto = Transformacao2D.DeMatriz(v[0], v[1], v[2], v[3], v[4], v[5]);
                    matrizDeTexto = matrizDeLinhaDeTexto;
                    break;
                }
                case "T*":
                    matrizDeTexto = matrizDeLinhaDeTexto;
                    break;
                case "Tj":
                    RegistrarTexto(pilhaOperandos.OfType<string>().LastOrDefault());
                    break;
                case "'":
                    matrizDeTexto = matrizDeLinhaDeTexto;
                    RegistrarTexto(pilhaOperandos.OfType<string>().LastOrDefault());
                    break;
                case "\"":
                    matrizDeTexto = matrizDeLinhaDeTexto;
                    RegistrarTexto(pilhaOperandos.OfType<string>().LastOrDefault());
                    break;
                case "TJ":
                {
                    var array = pilhaOperandos.OfType<List<object?>>().LastOrDefault();
                    if (array is not null)
                        RegistrarTexto(string.Concat(array.OfType<string>()));
                    break;
                }
                case "Do":
                {
                    var nome = pilhaOperandos.OfType<string>().LastOrDefault();
                    if (nome is not null)
                        ProcessarDo(nome, recursos, ctm, profundidade, doc, tracos, textos, avisos);
                    break;
                }
            }

            pilhaOperandos.Clear();
        }

        FinalizarPath(false); // path sem operador de pintura explícito no fim do stream (defensivo)
    }

    private static void ProcessarDo(
        string nomeXObject, Dictionary<string, object?>? recursos, Transformacao2D ctm, int profundidade,
        PdfDocumento doc, List<Traco> tracos, List<RotuloTexto> textos, List<string> avisos)
    {
        if (recursos is null)
            return;

        var xobjects = PdfDocumento.ResolverParaDicionario(doc, recursos.GetValueOrDefault("XObject"));
        if (xobjects is null || !xobjects.TryGetValue(nomeXObject, out var refObjeto))
            return;

        var resolvido = PdfDocumento.ResolverObjetoIndireto(doc, refObjeto);
        if (resolvido is not { Stream: not null } par)
            return;

        var (dictXObj, streamXObj) = par;
        var subtipo = dictXObj.TryGetValue("Subtype", out var st) ? st as string : null;

        if (subtipo != "Form")
            return; // Image (raster) — sem geometria vetorial, ignorado silenciosamente.

        byte[] decodificado;
        try
        {
            decodificado = PdfFiltros.Decodificar(dictXObj, streamXObj);
        }
        catch (Exception ex)
        {
            avisos.Add($"Falha ao decodificar XObject Form '{nomeXObject}': {ex.Message}");
            return;
        }

        var ctmForm = ctm;
        if (dictXObj.TryGetValue("Matrix", out var matrizObj) && matrizObj is List<object?> { Count: 6 } m)
        {
            var vals = m.Select(v => v is double d ? d : 0.0).ToArray();
            ctmForm = ctm.ComposicaoCom(Transformacao2D.DeMatriz(vals[0], vals[1], vals[2], vals[3], vals[4], vals[5]));
        }

        var recursosForm = PdfDocumento.ResolverParaDicionario(doc, dictXObj.GetValueOrDefault("Resources")) ?? recursos;

        Interpretar(Encoding.Latin1.GetString(decodificado), ctmForm, recursosForm, profundidade + 1, doc, tracos, textos, avisos);
    }
}
