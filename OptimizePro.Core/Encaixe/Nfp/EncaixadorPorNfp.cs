namespace OptimizePro.Core.Encaixe.Nfp;

/// <summary>
/// Um item pronto pro NFP, com o contorno pré-calculado em CADA rotação permitida (§11.11) —
/// mesma ideia de <c>ItemEncaixe.MascarasPorRotacao</c> do motor de contorno, só que em
/// polígono vetorial (não máscara de grade). Giro "fixa" tem só a chave 0; "mantém sentido" tem
/// 0/180; "livre" tem as 4. A rotação em si (<see cref="Geometria.Rotacionar90"/>) é responsabilidade
/// de quem monta o item (<c>EncaixeService</c>), não do encaixador.
/// </summary>
public sealed record ItemParaNfp(string Id, IReadOnlyDictionary<int, IReadOnlyList<PontoXY>> ContornosPorRotacao);

public sealed record PosicaoDeItemNfp(string ItemId, double X, double Y, int RotacaoGraus);

public sealed record ResultadoNfp(IReadOnlyList<PosicaoDeItemNfp> Posicoes, IReadOnlyList<string> ItensNaoEncaixados, double FundoMaximo);

/// <summary>
/// Porte de <c>encaixarPorNFP</c> (§11.5): para cada item, em CADA rotação permitida (§11.11,
/// portado 02/09/2026 — antes só rodava na orientação original), gera posições candidatas
/// (extremos do tecido, vértices dos NFPs contra as peças já colocadas, interseções
/// desses NFPs com as bordas do tecido), ordena por <c>(y, x)</c> — mais baixo e mais à
/// esquerda primeiro, para economizar tecido — e usa a primeira que não invade nenhuma
/// peça já colocada; a rotação final escolhida é a que ganhar entre todas as testadas.
/// </summary>
/// <remarks>
/// Duas simplificações conscientes frente à especificação original:
/// (1) não janela "últimas 25/4 peças" para gerar candidatas — usa TODAS as já colocadas,
/// mais lento em lotes grandes mas nunca gera candidatas piores que a versão janelada
/// (só descarta menos oportunidades de encaixe apertado);
/// (2) não gera candidatas de interseção ENTRE lados de NFPs de peças diferentes entre
/// si (só NFP×bordas do tecido) — um refinamento a mais para embretar ainda mais em
/// cenários bem apertados, deixado para depois se a qualidade pedir.
/// Nenhuma das duas afeta corretude (nunca gera sobreposição) — só o quão apertado fica.
/// Testar todas as rotações multiplica o custo de NFP por até 4x (giro "livre") — cada
/// rotação recalcula o NFP contra TODAS as peças já colocadas, do zero.
/// </remarks>
public static class EncaixadorPorNfp
{
    public static ResultadoNfp Encaixar(double larguraTecido, IReadOnlyList<ItemParaNfp> ordem)
    {
        var colocados = new List<(string Id, IReadOnlyList<PontoXY> Contorno, double X, double Y, int Rotacao)>();
        var naoEncaixados = new List<string>();
        var fundoMaximo = 0.0;

        foreach (var item in ordem)
        {
            // Testa CADA rotação permitida (§11.11) e fica com a melhor posição entre todas —
            // mesmo critério de desempate do original: mais baixo, depois mais à esquerda.
            (int Rotacao, PontoXY Pos, CaixaXY Caixa)? melhorEscolha = null;

            foreach (var (rotacao, contorno) in item.ContornosPorRotacao)
            {
                var caixa = Geometria.CaixaDeContorno(contorno);
                if (caixa.Largura > larguraTecido) continue;

                var nfpsMundiais = colocados
                    .SelectMany(c => CalculadoraDeNfp.Calcular(c.Contorno, contorno)
                        .Select(poligono => (IReadOnlyList<PontoXY>)[.. poligono.Select(p => new PontoXY(p.X + c.X, p.Y + c.Y))]))
                    .ToList();

                var candidatas = GerarCandidatas(larguraTecido, caixa, nfpsMundiais)
                    .OrderBy(p => p.Y)
                    .ThenBy(p => p.X);

                foreach (var candidata in candidatas)
                {
                    if (!DentroDoTecido(candidata, caixa, larguraTecido)) continue;
                    if (nfpsMundiais.Any(poligono => Geometria.PontoDentroDoPoligono(candidata, poligono))) continue;

                    if (melhorEscolha is null || candidata.Y < melhorEscolha.Value.Pos.Y ||
                        (candidata.Y == melhorEscolha.Value.Pos.Y && candidata.X < melhorEscolha.Value.Pos.X))
                        melhorEscolha = (rotacao, candidata, caixa);

                    break; // já é a melhor posição PARA ESSA rotação (candidatas vêm ordenadas) — vê a próxima rotação.
                }
            }

            if (melhorEscolha is not { } escolha)
            {
                naoEncaixados.Add(item.Id);
                continue;
            }

            colocados.Add((item.Id, item.ContornosPorRotacao[escolha.Rotacao], escolha.Pos.X, escolha.Pos.Y, escolha.Rotacao));

            var topoDoItem = escolha.Pos.Y + escolha.Caixa.MaxY;
            if (topoDoItem > fundoMaximo) fundoMaximo = topoDoItem;
        }

        var posicoes = colocados.Select(c => new PosicaoDeItemNfp(c.Id, c.X, c.Y, c.Rotacao)).ToList();
        return new ResultadoNfp(posicoes, naoEncaixados, fundoMaximo);
    }

    private static bool DentroDoTecido(PontoXY candidata, CaixaXY caixaLocal, double larguraTecido) =>
        candidata.X + caixaLocal.MinX >= -1e-9 &&
        candidata.X + caixaLocal.MaxX <= larguraTecido + 1e-9 &&
        candidata.Y + caixaLocal.MinY >= -1e-9;

    private static List<PontoXY> GerarCandidatas(double larguraTecido, CaixaXY caixaLocal, IReadOnlyList<IReadOnlyList<PontoXY>> nfpsMundiais)
    {
        var xEsquerda = -caixaLocal.MinX;
        var xDireita = larguraTecido - caixaLocal.MaxX;
        var yDeEncosto = -caixaLocal.MinY;

        List<PontoXY> candidatas = [new(xEsquerda, yDeEncosto), new(xDireita, yDeEncosto)];

        foreach (var poligono in nfpsMundiais)
        {
            candidatas.AddRange(poligono);

            for (var i = 0; i < poligono.Count; i++)
            {
                var p1 = poligono[i];
                var p2 = poligono[(i + 1) % poligono.Count];

                AdicionarIntersecaoComVertical(candidatas, p1, p2, xEsquerda);
                AdicionarIntersecaoComVertical(candidatas, p1, p2, xDireita);
            }
        }

        return candidatas;
    }

    private static void AdicionarIntersecaoComVertical(List<PontoXY> candidatas, PontoXY p1, PontoXY p2, double x)
    {
        if (Math.Abs(p2.X - p1.X) < 1e-9)
            return; // segmento vertical — sem interseção única com outra vertical

        if ((p1.X - x) * (p2.X - x) > 1e-9)
            return; // os dois extremos do lado ficam do mesmo lado de x — não cruza

        var t = (x - p1.X) / (p2.X - p1.X);
        if (t is < 0 or > 1)
            return;

        candidatas.Add(new PontoXY(x, p1.Y + t * (p2.Y - p1.Y)));
    }
}
