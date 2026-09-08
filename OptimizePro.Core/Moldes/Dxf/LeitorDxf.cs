using OptimizePro.Core.Moldes;

namespace OptimizePro.Core.Moldes.Dxf;

/// <summary>
/// Leitor de arquivos DXF (ASCII) — §4.2/§8.1 do documento de arquitetura. Extrai
/// traços/textos das entidades, costura e classifica em peças/furos via
/// <see cref="MontagemDeMoldes"/>, converte para cm e nomeia peças pelo texto que
/// cai dentro do contorno.
/// </summary>
public sealed class LeitorDxf : ILeitorDeMolde
{
    public bool SuportaExtensao(string extensao) =>
        string.Equals(extensao.TrimStart('.'), "dxf", StringComparison.OrdinalIgnoreCase);

    public Task<ResultadoLeituraMolde> LerAsync(Stream conteudo, OpcoesLeituraMolde opcoes)
    {
        var avisos = new List<string>();

        var pares = DxfLeitorDePares.Ler(conteudo);
        if (pares is null)
            return Task.FromResult(new ResultadoLeituraMolde([], "desconhecida", avisos, "Arquivo DXF binário não é suportado — salve como DXF ASCII."));

        var documento = DxfAnalisadorDeSecoes.Analisar(pares);

        var tracos = new List<Traco>();
        var textos = new List<RotuloTexto>();
        var conversor = new DxfConversor(documento.Blocos, avisos);

        foreach (var entidade in documento.EntidadesTopo)
            conversor.Converter(entidade, Transformacao2D.Identidade, 0, tracos, textos);

        if (tracos.Count == 0)
            return Task.FromResult(new ResultadoLeituraMolde([], "desconhecida", avisos, "Nenhuma geometria reconhecida no arquivo DXF."));

        var caixaGeral = Geometria.CaixaDeContorno([.. tracos.SelectMany(t => t.Pontos)]);
        var maiorLado = Math.Max(caixaGeral.Largura, caixaGeral.Altura);

        documento.VariaveisDeHeader.TryGetValue("$INSUNITS", out var insunits);
        var (fatorParaCm, nomeUnidade) = ResolverUnidade(opcoes.UnidadeForcada, insunits, maiorLado, avisos);

        // DXF cresce para cima (Y+); o resto do pipeline (canvas/Encaixe) espera Y para
        // baixo — inverte em torno do próprio desenho antes de converter para cm.
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

    /// <summary>
    /// Prioridade: unidade forçada pelo usuário > <c>$INSUNITS</c> do cabeçalho > chute
    /// pelo tamanho da bbox (§8.1: {@code >300 -> mm, >12 -> cm, senão polegada}, em
    /// unidades cruas do arquivo).
    /// </summary>
    private static (double Fator, string Nome) ResolverUnidade(string? unidadeForcada, string? insunits, double maiorLadoBBox, List<string> avisos)
    {
        if (!string.IsNullOrWhiteSpace(unidadeForcada))
        {
            var fatorForcado = FatorPorNome(unidadeForcada);
            if (fatorForcado is not null)
                return (fatorForcado.Value, unidadeForcada);

            avisos.Add($"Unidade forçada '{unidadeForcada}' não reconhecida — ignorando.");
        }

        if (int.TryParse(insunits, out var codigo))
        {
            var resolvidoPorCodigo = codigo switch
            {
                1 => (Fator: 2.54, Nome: "pol"),
                2 => (Fator: 30.48, Nome: "pé"),
                4 => (Fator: 0.1, Nome: "mm"),
                5 => (Fator: 1.0, Nome: "cm"),
                6 => (Fator: 100.0, Nome: "m"),
                _ => ((double Fator, string Nome)?)null,
            };

            if (resolvidoPorCodigo is { } r)
                return r;
        }

        avisos.Add("Unidade do DXF não informada ($INSUNITS ausente/desconhecido) — estimada pelo tamanho do desenho.");

        if (maiorLadoBBox > 300) return (0.1, "mm (estimado)");
        if (maiorLadoBBox > 12) return (1.0, "cm (estimado)");
        return (2.54, "pol (estimado)");
    }

    private static double? FatorPorNome(string unidade) => unidade.Trim().ToLowerInvariant() switch
    {
        "mm" or "milimetro" or "milímetro" => 0.1,
        "cm" or "centimetro" or "centímetro" => 1.0,
        "m" or "metro" => 100.0,
        "pol" or "polegada" or "in" or "inch" => 2.54,
        "pe" or "pé" or "ft" or "feet" => 30.48,
        _ => null,
    };
}
