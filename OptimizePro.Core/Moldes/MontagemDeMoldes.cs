namespace OptimizePro.Core.Moldes;

/// <summary>
/// Porte de <c>montarLacos</c>/<c>separarPecasEFuros</c> de <c>public/moldes.js</c> (§8.1
/// da especificação) — pipeline comum a todos os leitores de formato (DXF/PLT/SVG/PDF):
/// costura traços soltos em contornos fechados, depois classifica cada laço como peça
/// ou furo. Opera na unidade original do arquivo; conversão para cm e nomeação por
/// texto acontecem no leitor específico, que conhece a unidade e os textos do arquivo.
/// </summary>
/// <remarks>
/// A especificação descreve o comportamento em alto nível, não a implementação
/// original linha a linha (não disponível neste porte). Onde uma regra era ambígua
/// (ex.: qual laço "pai" escolher quando vários o contêm), a escolha feita está
/// documentada nos comentários do método — validar contra o sistema atual (§18 da
/// arquitetura: testes de parser comparando contorno/furos/medida) antes de confiar
/// em casos de borda.
/// </remarks>
public static class MontagemDeMoldes
{
    /// <summary>Um laço já é considerado "a folha"/moldura se cobre esta fração da bbox geral.</summary>
    public const double LimiarCoberturaDaFolha = 0.90;

    /// <summary>...e contém outro laço com pelo menos esta fração da área da própria folha.</summary>
    /// <remarks>
    /// Ambiguidade conhecida (não resolvível sem o código-fonte original): uma peça cujo
    /// contorno externo é (quase) um retângulo perfeito, cobrindo toda a bbox do desenho,
    /// e que tem um furo funcional ≥15% da própria área, bate nos dois critérios de
    /// "folha" e seria descartada por engano — mesmo sendo uma peça legítima com furo
    /// grande (ex.: um cós/faixa com um recorte grande). Validar contra arquivos DXF
    /// reais do sistema atual; se o caso ocorrer, um critério adicional plausível é exigir
    /// que a "folha" tenha pelo menos 2 laços filhos diretos (várias peças soltas dentro
    /// da moldura), não apenas 1.
    /// </remarks>
    public const double LimiarConteudoDaFolha = 0.15;

    /// <summary>
    /// Costura traços soltos em cadeias contínuas por proximidade de pontas, dentro de uma
    /// tolerância relativa ao tamanho geral do desenho (§8.1 "montarLacos").
    /// </summary>
    public static IReadOnlyList<LacoCosturado> MontarLacos(IReadOnlyList<Traco> tracos)
    {
        var pendentes = tracos
            .Where(t => t.Pontos.Count >= 2)
            .Select(t => new List<PontoXY>(t.Pontos))
            .ToList();

        if (pendentes.Count == 0)
            return [];

        var todosOsPontos = pendentes.SelectMany(p => p);
        var caixaGeral = Geometria.CaixaDeContorno(todosOsPontos.ToList());
        var maiorLado = Math.Max(caixaGeral.Largura, caixaGeral.Altura);
        var tolerancia = Math.Max(maiorLado * 5e-4, 1e-6);

        var lacos = new List<LacoCosturado>();

        while (pendentes.Count > 0)
        {
            var cadeia = pendentes[0];
            pendentes.RemoveAt(0);

            bool progrediu;
            do
            {
                progrediu = false;
                if (EstaFechada(cadeia, tolerancia))
                    break;

                var fim = cadeia[^1];

                for (var i = 0; i < pendentes.Count; i++)
                {
                    var candidato = pendentes[i];

                    if (Geometria.DistanciaEntre(fim, candidato[0]) <= tolerancia)
                    {
                        cadeia.AddRange(candidato.Skip(1));
                        pendentes.RemoveAt(i);
                        progrediu = true;
                        break;
                    }

                    if (Geometria.DistanciaEntre(fim, candidato[^1]) <= tolerancia)
                    {
                        candidato.Reverse();
                        cadeia.AddRange(candidato.Skip(1));
                        pendentes.RemoveAt(i);
                        progrediu = true;
                        break;
                    }
                }
            } while (progrediu);

            lacos.Add(new LacoCosturado(cadeia, EstaFechada(cadeia, tolerancia)));
        }

        return lacos;
    }

    private static bool EstaFechada(List<PontoXY> cadeia, double tolerancia)
        => cadeia.Count >= 3 && Geometria.DistanciaEntre(cadeia[0], cadeia[^1]) <= tolerancia;

    /// <summary>
    /// Classifica laços fechados como peça (contorno externo) ou furo (dentro de uma peça),
    /// remove duplicatas e descarta a "folha"/moldura de página (§8.1 "separarPecasEFuros").
    /// Laços que não fecharam são ignorados aqui — cabe ao leitor decidir se isso vira aviso.
    /// </summary>
    public static IReadOnlyList<PecaBruta> SepararPecasEFuros(IReadOnlyList<LacoCosturado> lacos)
    {
        var fechados = lacos.Where(l => l.Fechado && l.Pontos.Count >= 3).ToList();
        if (fechados.Count == 0)
            return [];

        var infos = fechados
            .Select(l => new LacoInfo(l.Pontos, Math.Abs(Geometria.AreaComSinal(l.Pontos)), Geometria.CaixaDeContorno(l.Pontos)))
            .Where(info => info.Area > Geometria.GeoEpsilon)
            .ToList();

        infos = TirarRepetidos(infos);

        var caixaGeral = Geometria.CaixaDeContorno(infos.SelectMany(i => i.Pontos).ToList());
        var areaCaixaGeral = caixaGeral.Largura * caixaGeral.Altura;

        var folha = infos.FirstOrDefault(info =>
            areaCaixaGeral > 0 &&
            (info.Caixa.Largura * info.Caixa.Altura) / areaCaixaGeral >= LimiarCoberturaDaFolha &&
            infos.Any(outro => outro != info && outro.Area >= info.Area * LimiarConteudoDaFolha));

        if (folha is not null)
            infos.Remove(folha);

        // Maior área primeiro: peças candidatas a "pai" precisam já existir quando um
        // laço menor é avaliado.
        infos = [.. infos.OrderByDescending(i => i.Area)];

        var pecas = new List<PecaComFuros>();

        foreach (var laco in infos)
        {
            var pontoDeTeste = laco.Pontos[0];

            // Entre as peças já aceitas cujo contorno contém este laço, escolhe a de
            // MENOR área (pai mais próximo/imediato) — necessário para aninhamento
            // correto quando há mais de um nível (peça dentro de furo de outra peça).
            // Um ponto que já caiu dentro de um furo registrado não conta: aquela
            // região deixou de ser material da peça, então um laço ali é uma NOVA
            // peça solta dentro do furo, não mais um furo da mesma peça.
            var pai = pecas
                .Where(p => Geometria.PontoDentroDoPoligono(pontoDeTeste, p.Contorno)
                    && !p.Furos.Any(furo => Geometria.PontoDentroDoPoligono(pontoDeTeste, furo)))
                .OrderBy(p => p.Area)
                .FirstOrDefault();

            if (pai is not null)
            {
                pai.Furos.Add(laco.Pontos);
            }
            else
            {
                pecas.Add(new PecaComFuros(laco.Pontos, laco.Area, []));
            }
        }

        return [.. pecas.Select(p => new PecaBruta(p.Contorno, p.Furos))];
    }

    private static List<LacoInfo> TirarRepetidos(List<LacoInfo> infos)
    {
        var resultado = new List<LacoInfo>();

        foreach (var info in infos)
        {
            var duplicado = resultado.Any(existente => SaoEquivalentes(existente, info));
            if (!duplicado)
                resultado.Add(info);
        }

        return resultado;
    }

    private static bool SaoEquivalentes(LacoInfo a, LacoInfo b)
    {
        var maiorArea = Math.Max(a.Area, b.Area);
        if (maiorArea <= Geometria.GeoEpsilon)
            return true;

        var diferencaDeArea = Math.Abs(a.Area - b.Area) / maiorArea;
        if (diferencaDeArea > 1e-6)
            return false;

        var toleranciaCaixa = Math.Max(a.Caixa.Largura, a.Caixa.Altura) * 1e-4;
        return Math.Abs(a.Caixa.MinX - b.Caixa.MinX) <= toleranciaCaixa
            && Math.Abs(a.Caixa.MinY - b.Caixa.MinY) <= toleranciaCaixa
            && Math.Abs(a.Caixa.MaxX - b.Caixa.MaxX) <= toleranciaCaixa
            && Math.Abs(a.Caixa.MaxY - b.Caixa.MaxY) <= toleranciaCaixa;
    }

    /// <summary>
    /// Passo final comum a todos os leitores de formato: converte <see cref="PecaBruta"/>
    /// (já em cm) para <see cref="PecaLida"/>, nomeando pelo primeiro texto cujo ponto cai
    /// dentro do contorno e descartando peças com lado menor ≤ <paramref name="ladoMinimoCm"/>
    /// (§8.1 passo 5).
    /// </summary>
    public static IReadOnlyList<PecaLida> MontarPecasLidas(
        IReadOnlyList<PecaBruta> pecasBrutas, IReadOnlyList<RotuloTexto> textos, double ladoMinimoCm = 0.2)
    {
        var pecas = new List<PecaLida>();

        foreach (var bruta in pecasBrutas)
        {
            var caixa = Geometria.CaixaDeContorno(bruta.Contorno);
            var ladoMenor = Math.Min(caixa.Largura, caixa.Altura);
            if (ladoMenor <= ladoMinimoCm)
                continue;

            var nome = textos
                .FirstOrDefault(t => Geometria.PontoDentroDoPoligono(new PontoXY(t.X, t.Y), bruta.Contorno))
                ?.Texto ?? $"peça {pecas.Count + 1}";

            pecas.Add(new PecaLida(nome, bruta.Contorno, bruta.Furos, caixa.Largura, caixa.Altura));
        }

        return pecas;
    }

    private sealed record LacoInfo(IReadOnlyList<PontoXY> Pontos, double Area, CaixaXY Caixa);

    private sealed class PecaComFuros(IReadOnlyList<PontoXY> contorno, double area, List<IReadOnlyList<PontoXY>> furos)
    {
        public IReadOnlyList<PontoXY> Contorno { get; } = contorno;
        public double Area { get; } = area;
        public List<IReadOnlyList<PontoXY>> Furos { get; } = furos;
    }
}
