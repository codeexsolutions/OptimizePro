using System.Text;

namespace OptimizePro.Core.Moldes.Pdf;

/// <summary>
/// Leitor de arquivos PDF vetoriais — §4.2/§8.1 do documento de arquitetura. Em vez de
/// seguir a xref/trailer/Catalog (frágil se corrompida), localiza diretamente todos os
/// objetos com <c>/Type /Page</c> (mais robusto — mesma filosofia "sem usar a tabela
/// xref" da especificação), interpreta o content stream de cada uma e alimenta
/// <see cref="MontagemDeMoldes"/> como os demais leitores.
/// </summary>
public sealed class LeitorPdf : ILeitorDeMolde
{
    private const double PdfPtPorCm = 72.0 / 2.54;
    private const int ProfundidadeMaximaDeHeranca = 8;

    public bool SuportaExtensao(string extensao) =>
        string.Equals(extensao.TrimStart('.'), "pdf", StringComparison.OrdinalIgnoreCase);

    public Task<ResultadoLeituraMolde> LerAsync(Stream conteudo, OpcoesLeituraMolde opcoes)
    {
        var avisos = new List<string>();

        using var memoria = new MemoryStream();
        conteudo.CopyTo(memoria);
        var texto = Encoding.Latin1.GetString(memoria.ToArray());

        if (texto.Contains("/Encrypt", StringComparison.Ordinal))
            return Task.FromResult(new ResultadoLeituraMolde([], "desconhecida", avisos, "PDF protegido por senha (/Encrypt) não é suportado."));

        var doc = PdfAnalisadorDeDocumento.Analisar(texto, avisos);

        var paginas = doc.Objetos.Values
            .Where(o => PdfDocumento.NomeDoTipo(o.Dicionario) == "Page")
            .Select(o => o.Dicionario)
            .ToList();

        if (paginas.Count == 0)
            return Task.FromResult(new ResultadoLeituraMolde([], "desconhecida", avisos, "Nenhuma página encontrada no PDF (arquivo corrompido ou não é um PDF válido)."));

        var tracos = new List<Traco>();
        var textos = new List<RotuloTexto>();

        foreach (var pagina in paginas)
        {
            try
            {
                InterpretarPagina(pagina, doc, tracos, textos, avisos);
            }
            catch (Exception ex)
            {
                avisos.Add($"Falha ao interpretar uma página do PDF: {ex.Message}");
            }
        }

        if (tracos.Count == 0)
            return Task.FromResult(new ResultadoLeituraMolde([], "desconhecida", avisos, "Nenhuma geometria vetorial reconhecida (PDF pode ser escaneado/raster)."));

        var (fatorParaCm, nomeUnidade) = ResolverUnidade(opcoes.UnidadeForcada);

        // PDF cresce para cima (como DXF/PLT) — inverte Y para o espaço de trabalho.
        PontoXY ParaEspacoDeTrabalho(PontoXY p) => new(p.X * fatorParaCm, -p.Y * fatorParaCm);

        var tracosCm = tracos.Select(t => new Traco([.. t.Pontos.Select(ParaEspacoDeTrabalho)], t.Fechada)).ToList();
        var textosCm = textos.Select(r => new RotuloTexto(r.Texto, r.X * fatorParaCm, -r.Y * fatorParaCm)).ToList();

        var lacos = MontagemDeMoldes.MontarLacos(tracosCm);
        var quantidadeAbertos = lacos.Count(l => !l.Fechado);
        if (quantidadeAbertos > 0)
            avisos.Add($"{quantidadeAbertos} traço(s) não formaram contorno fechado e foram ignorados.");

        var pecasBrutas = MontagemDeMoldes.SepararPecasEFuros(lacos);
        var pecas = MontagemDeMoldes.MontarPecasLidas(pecasBrutas, textosCm);

        return Task.FromResult(new ResultadoLeituraMolde(pecas, nomeUnidade, avisos, null));
    }

    private static void InterpretarPagina(
        Dictionary<string, object?> pagina, PdfDocumento doc, List<Traco> tracos, List<RotuloTexto> textos, List<string> avisos)
    {
        var conteudoConcatenado = ObterConteudoConcatenado(pagina, doc, avisos);
        if (conteudoConcatenado is null)
            return;

        var recursos = ObterRecursosHerdados(pagina, doc);
        PdfConteudoInterpretador.Interpretar(conteudoConcatenado, Transformacao2D.Identidade, recursos, 0, doc, tracos, textos, avisos);
    }

    private static string? ObterConteudoConcatenado(Dictionary<string, object?> pagina, PdfDocumento doc, List<string> avisos)
    {
        if (!pagina.TryGetValue("Contents", out var contents) || contents is null)
            return null;

        var referencias = contents is List<object?> lista ? lista : [contents];
        var partes = new List<string>();

        foreach (var refObjeto in referencias)
        {
            var resolvido = PdfDocumento.ResolverObjetoIndireto(doc, refObjeto);
            if (resolvido is not { Stream: not null } par)
                continue;

            try
            {
                var decodificado = PdfFiltros.Decodificar(par.Dicionario, par.Stream);
                partes.Add(Encoding.Latin1.GetString(decodificado));
            }
            catch (Exception ex)
            {
                avisos.Add($"Falha ao decodificar content stream da página: {ex.Message}");
            }
        }

        return partes.Count > 0 ? string.Join(' ', partes) : null;
    }

    /// <summary><c>/Resources</c> é herdável do nó <c>/Pages</c> pai quando ausente na página (§8.1).</summary>
    private static Dictionary<string, object?>? ObterRecursosHerdados(Dictionary<string, object?> pagina, PdfDocumento doc)
    {
        var atual = pagina;
        for (var i = 0; i < ProfundidadeMaximaDeHeranca; i++)
        {
            var recursos = PdfDocumento.ResolverParaDicionario(doc, atual.GetValueOrDefault("Resources"));
            if (recursos is not null)
                return recursos;

            var pai = PdfDocumento.ResolverParaDicionario(doc, atual.GetValueOrDefault("Parent"));
            if (pai is null)
                return null;

            atual = pai;
        }

        return null;
    }

    private static (double Fator, string Nome) ResolverUnidade(string? unidadeForcada)
    {
        if (!string.IsNullOrWhiteSpace(unidadeForcada))
        {
            var forcado = FatorPorNome(unidadeForcada);
            if (forcado is not null)
                return (forcado.Value, unidadeForcada);
        }

        return (1.0 / PdfPtPorCm, "pt"); // §8.1: unidade nativa do PDF é sempre pontos.
    }

    private static double? FatorPorNome(string unidade) => unidade.Trim().ToLowerInvariant() switch
    {
        "pt" => 1.0 / PdfPtPorCm,
        "mm" => 0.1,
        "cm" => 1.0,
        "m" or "metro" => 100.0,
        "in" or "pol" or "polegada" or "inch" => 2.54,
        _ => null,
    };
}
