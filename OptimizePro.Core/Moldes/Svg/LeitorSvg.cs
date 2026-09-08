using System.Globalization;
using System.Xml.Linq;

namespace OptimizePro.Core.Moldes.Svg;

/// <summary>
/// Leitor de arquivos SVG — §4.2/§8.1 do documento de arquitetura. Diferente do sistema
/// atual (que delega a `DOMParser`/`getPointAtLength` do navegador), este leitor faz seu
/// próprio path-flattening: percorre a árvore XML acumulando transformações, resolve
/// &lt;use&gt; (clone + transform, com proteção contra ciclo e profundidade máxima),
/// ignora &lt;defs&gt;/&lt;clipPath&gt;/&lt;mask&gt;/&lt;symbol&gt; como alvos diretos de
/// renderização (mas resolve &lt;use&gt; que aponte para dentro deles), e alimenta
/// <see cref="MontagemDeMoldes"/> como os demais leitores.
/// </summary>
public sealed class LeitorSvg : ILeitorDeMolde
{
    private const int ProfundidadeMaximaDeUse = 8;
    private static readonly XNamespace NsXlink = "http://www.w3.org/1999/xlink";

    public bool SuportaExtensao(string extensao) =>
        string.Equals(extensao.TrimStart('.'), "svg", StringComparison.OrdinalIgnoreCase);

    public Task<ResultadoLeituraMolde> LerAsync(Stream conteudo, OpcoesLeituraMolde opcoes)
    {
        var avisos = new List<string>();

        XDocument documento;
        try
        {
            documento = XDocument.Load(conteudo);
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ResultadoLeituraMolde([], "desconhecida", avisos, $"Arquivo SVG inválido: {ex.Message}"));
        }

        var raiz = documento.Root;
        if (raiz is null || raiz.Name.LocalName != "svg")
            return Task.FromResult(new ResultadoLeituraMolde([], "desconhecida", avisos, "Arquivo não é um SVG válido (elemento raiz <svg> não encontrado)."));

        var porId = raiz.DescendantsAndSelf()
            .Where(e => e.Attribute("id") is not null)
            .GroupBy(e => (string)e.Attribute("id")!)
            .ToDictionary(g => g.Key, g => g.First());

        var tracos = new List<Traco>();
        var textos = new List<RotuloTexto>();

        Percorrer(raiz, Transformacao2D.Identidade, 0, porId, tracos, textos, [], avisos);

        if (tracos.Count == 0)
            return Task.FromResult(new ResultadoLeituraMolde([], "desconhecida", avisos, "Nenhuma geometria reconhecida no arquivo SVG."));

        var (fatorParaCm, nomeUnidade) = ResolverUnidadeEEscala(raiz, opcoes.UnidadeForcada, avisos);

        // SVG já cresce para baixo (mesma convenção do espaço de trabalho usado pelos
        // demais leitores) — diferente de DXF/PLT, aqui só escala, sem inverter Y.
        PontoXY ParaEspacoDeTrabalho(PontoXY p) => new(p.X * fatorParaCm, p.Y * fatorParaCm);

        var tracosCm = tracos.Select(t => new Traco([.. t.Pontos.Select(ParaEspacoDeTrabalho)], t.Fechada)).ToList();
        var textosCm = textos.Select(r => new RotuloTexto(r.Texto, r.X * fatorParaCm, r.Y * fatorParaCm)).ToList();

        var lacos = MontagemDeMoldes.MontarLacos(tracosCm);
        var quantidadeAbertos = lacos.Count(l => !l.Fechado);
        if (quantidadeAbertos > 0)
            avisos.Add($"{quantidadeAbertos} traço(s) não formaram contorno fechado e foram ignorados.");

        var pecasBrutas = MontagemDeMoldes.SepararPecasEFuros(lacos);
        var pecas = MontagemDeMoldes.MontarPecasLidas(pecasBrutas, textosCm);

        return Task.FromResult(new ResultadoLeituraMolde(pecas, nomeUnidade, avisos, null));
    }

    private static void Percorrer(
        XElement elemento, Transformacao2D transformAcumulada, int profundidade,
        IReadOnlyDictionary<string, XElement> porId, List<Traco> tracos, List<RotuloTexto> textos,
        HashSet<XElement> pilhaDeUse, List<string> avisos)
    {
        var nomeLocal = elemento.Name.LocalName;

        if (nomeLocal is "defs" or "clipPath" or "mask" or "symbol")
            return; // só acessível via <use> — não renderiza direto

        var transformAtual = transformAcumulada.ComposicaoCom(
            SvgAnalisadorDeTransformacoes.Analisar((string?)elemento.Attribute("transform")));

        switch (nomeLocal)
        {
            case "path":
            {
                var d = (string?)elemento.Attribute("d");
                if (!string.IsNullOrWhiteSpace(d))
                {
                    foreach (var sub in SvgAnalisadorDePath.Analisar(d, avisos))
                        tracos.Add(new Traco([.. sub.Pontos.Select(transformAtual.Aplicar)], sub.Fechada));
                }
                return;
            }

            case "rect":
            {
                var pts = SvgFormas.Retangulo(
                    AtributoNumerico(elemento, "x"), AtributoNumerico(elemento, "y"),
                    AtributoNumerico(elemento, "width"), AtributoNumerico(elemento, "height"),
                    AtributoNumerico(elemento, "rx"), AtributoNumerico(elemento, "ry"));
                if (pts.Count >= 2)
                    tracos.Add(new Traco([.. pts.Select(transformAtual.Aplicar)], true));
                return;
            }

            case "circle":
            {
                var pts = SvgFormas.Circulo(AtributoNumerico(elemento, "cx"), AtributoNumerico(elemento, "cy"), AtributoNumerico(elemento, "r"));
                if (pts.Count >= 2)
                    tracos.Add(new Traco([.. pts.Select(transformAtual.Aplicar)], true));
                return;
            }

            case "ellipse":
            {
                var pts = SvgFormas.Elipse(
                    AtributoNumerico(elemento, "cx"), AtributoNumerico(elemento, "cy"),
                    AtributoNumerico(elemento, "rx"), AtributoNumerico(elemento, "ry"));
                if (pts.Count >= 2)
                    tracos.Add(new Traco([.. pts.Select(transformAtual.Aplicar)], true));
                return;
            }

            case "line":
            {
                var pts = SvgFormas.Linha(
                    AtributoNumerico(elemento, "x1"), AtributoNumerico(elemento, "y1"),
                    AtributoNumerico(elemento, "x2"), AtributoNumerico(elemento, "y2"));
                tracos.Add(new Traco([.. pts.Select(transformAtual.Aplicar)], false));
                return;
            }

            case "polyline":
            case "polygon":
            {
                var pontosAttr = (string?)elemento.Attribute("points");
                var pts = pontosAttr is not null ? SvgFormas.PontosDeLista(pontosAttr) : null;
                if (pts is { Count: >= 2 })
                {
                    var fechado = nomeLocal == "polygon";
                    var lista = pts.ToList();
                    if (fechado) lista.Add(lista[0]);
                    tracos.Add(new Traco([.. lista.Select(transformAtual.Aplicar)], fechado));
                }
                return;
            }

            case "text":
            {
                var texto = string.Concat(elemento.DescendantNodes().OfType<XText>().Select(t => t.Value)).Trim();
                if (!string.IsNullOrEmpty(texto))
                {
                    var p = transformAtual.Aplicar(new PontoXY(AtributoNumerico(elemento, "x"), AtributoNumerico(elemento, "y")));
                    textos.Add(new RotuloTexto(texto, p.X, p.Y));
                }
                return;
            }

            case "use":
            {
                ResolverUse(elemento, transformAtual, profundidade, porId, tracos, textos, pilhaDeUse, avisos);
                return;
            }
        }

        foreach (var filho in elemento.Elements())
            Percorrer(filho, transformAtual, profundidade, porId, tracos, textos, pilhaDeUse, avisos);
    }

    private static void ResolverUse(
        XElement elementoUse, Transformacao2D transformAtual, int profundidade,
        IReadOnlyDictionary<string, XElement> porId, List<Traco> tracos, List<RotuloTexto> textos,
        HashSet<XElement> pilhaDeUse, List<string> avisos)
    {
        var href = (string?)elementoUse.Attribute(NsXlink + "href") ?? (string?)elementoUse.Attribute("href");

        if (string.IsNullOrWhiteSpace(href) || !href.StartsWith('#'))
        {
            avisos.Add("Elemento <use> sem referência local válida (href) — ignorado.");
            return;
        }

        var idRef = href[1..];
        if (!porId.TryGetValue(idRef, out var alvo))
        {
            avisos.Add($"<use> referencia id '#{idRef}' não encontrado — ignorado.");
            return;
        }

        if (profundidade >= ProfundidadeMaximaDeUse)
        {
            avisos.Add($"Profundidade máxima de <use> ({ProfundidadeMaximaDeUse}) excedida em '#{idRef}' — ignorado.");
            return;
        }

        if (!pilhaDeUse.Add(alvo))
        {
            avisos.Add($"<use> cíclico detectado em '#{idRef}' — ignorado.");
            return;
        }

        var transformComOffset = transformAtual.ComposicaoCom(
            Transformacao2D.DeEscalaRotacaoTranslacao(1, 1, 0, AtributoNumerico(elementoUse, "x"), AtributoNumerico(elementoUse, "y")));

        Percorrer(alvo, transformComOffset, profundidade + 1, porId, tracos, textos, pilhaDeUse, avisos);

        pilhaDeUse.Remove(alvo);
    }

    private static double AtributoNumerico(XElement elemento, string nome)
    {
        var valor = (string?)elemento.Attribute(nome);
        return valor is not null && double.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    /// <summary>
    /// Prioridade: unidade forçada > width/height (com unidade) do &lt;svg&gt; raiz,
    /// escalado por viewBox quando presente > 96 px/polegada CSS com aviso (§8.1).
    /// </summary>
    private static (double FatorParaCm, string NomeUnidade) ResolverUnidadeEEscala(XElement raiz, string? unidadeForcada, List<string> avisos)
    {
        if (!string.IsNullOrWhiteSpace(unidadeForcada))
        {
            var forcado = FatorPorNome(unidadeForcada);
            if (forcado is not null)
                return (forcado.Value, unidadeForcada);

            avisos.Add($"Unidade forçada '{unidadeForcada}' não reconhecida — ignorando.");
        }

        double? viewBoxLargura = null;
        var viewBoxAttr = (string?)raiz.Attribute("viewBox");
        if (!string.IsNullOrWhiteSpace(viewBoxAttr))
        {
            var partes = viewBoxAttr.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 4 && double.TryParse(partes[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var vbw))
                viewBoxLargura = vbw;
        }

        var larguraAttr = (string?)raiz.Attribute("width");
        if (!string.IsNullOrWhiteSpace(larguraAttr) && TentarAnalisarComprimento(larguraAttr, out var valorLargura, out var unidadeLargura))
        {
            if (unidadeLargura.Length == 0)
                avisos.Add("SVG com width/height sem unidade explícita no elemento raiz — assumindo 96 px/polegada (CSS).");

            var fatorDaUnidade = FatorPorNome(unidadeLargura) ?? 2.54 / 96.0;
            var nomeUnidade = unidadeLargura.Length > 0 ? unidadeLargura : "px";

            if (viewBoxLargura is > 0)
                return (valorLargura * fatorDaUnidade / viewBoxLargura.Value, nomeUnidade);

            return (fatorDaUnidade, nomeUnidade);
        }

        avisos.Add("SVG sem width/height com unidade explícita no elemento raiz — assumindo 96 px/polegada (CSS).");
        return (2.54 / 96.0, "px (estimado)");
    }

    private static bool TentarAnalisarComprimento(string valor, out double numero, out string unidade)
    {
        valor = valor.Trim();
        var i = valor.Length;
        while (i > 0 && !char.IsAsciiDigit(valor[i - 1]) && valor[i - 1] != '.')
            i--;

        unidade = valor[i..].Trim().ToLowerInvariant();
        return double.TryParse(valor[..i], NumberStyles.Float, CultureInfo.InvariantCulture, out numero);
    }

    private static double? FatorPorNome(string unidade) => unidade.Trim().ToLowerInvariant() switch
    {
        "px" or "" => 2.54 / 96.0,
        "mm" => 0.1,
        "cm" => 1.0,
        "m" or "metro" => 100.0,
        "in" or "pol" or "polegada" or "inch" => 2.54,
        "pt" => 2.54 / 72.0,
        "pc" => 2.54 / 6.0,
        _ => null,
    };
}
