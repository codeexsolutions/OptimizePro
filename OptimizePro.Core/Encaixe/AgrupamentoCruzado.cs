namespace OptimizePro.Core.Encaixe;

/// <summary>Um jeito de juntar duas peças de FORMATOS diferentes num bloco só (§11.8, "cruzada") — as formas úteis (abaixo do limiar de utilidade) e a economia de área que renderam.</summary>
public sealed record ArranjoCruzado(IReadOnlyList<Forma> Formas, double AreaSolta, double MelhorArea)
{
    /// <summary>Fração de área economizada vs as duas peças soltas — usado pra ordenar qual par vale mais a pena casar primeiro.</summary>
    public double Economia => AreaSolta > 0 ? 1 - MelhorArea / AreaSolta : 0;
}

/// <summary>
/// Porte de <c>formasDoBlocoMisto</c> (§11.8, "cruzada") — o mesmo truque da dupla/trio
/// (<see cref="AgrupamentoDeBlocos"/>), mas juntando peças de FORMATOS diferentes: a peça
/// pequena entrando no vão da peça grande, em vez de entrar no vão da cópia dela mesma.
/// </summary>
public static class AgrupamentoCruzado
{
    public const double FatorDeUtilidade = 0.98;

    /// <summary>
    /// Tenta juntar UMA peça A com UMA peça B, cada uma na rotação que aceita — testa nos dois
    /// sentidos (A parada e B deslizando; depois o contrário), porque o vão de uma pode receber
    /// a outra bem de um lado e mal do outro. Devolve <c>null</c> se nenhum arranjo compensar.
    /// Os <see cref="Forma.Partes"/> de todo arranjo devolvido saem sempre na ordem canônica
    /// [A, B] — mesmo quando B venceu como "base" internamente — pra quem chama poder mapear
    /// <c>Partes[0]</c>/<c>Partes[1]</c> direto pras cópias reais de A/B sem ambiguidade.
    /// </summary>
    public static ArranjoCruzado? Tentar(
        IReadOnlyDictionary<int, Mascara> mascarasA, IReadOnlyDictionary<int, Mascara> mascarasB)
    {
        var arranjos = new List<Forma>();

        void TentarDirecao(IReadOnlyDictionary<int, Mascara> baseMascaras, IReadOnlyDictionary<int, Mascara> movelMascaras, bool baseEhA)
        {
            foreach (var (rotBase, mascaraBase) in baseMascaras)
            {
                var bloco = Forma.DeMascaraUnica(mascaraBase, rotBase);
                Forma? melhor = null;
                var melhorArea = double.MaxValue;

                foreach (var (rotMovel, mascaraMovel) in movelMascaras)
                {
                    var candidato = EncostoDeFormas.EncostarNaForma(bloco, mascaraMovel, rotMovel);
                    var area = (double)candidato.Colunas * (candidato.MaxBase + 1);
                    if (area < melhorArea)
                    {
                        melhorArea = area;
                        melhor = candidato;
                    }
                }

                if (melhor is null) continue;

                // Canoniza pra [A, B]: quando B foi a base, Partes saiu [B, A] — inverte.
                var partesCanonicas = baseEhA ? melhor.Partes : [melhor.Partes[1], melhor.Partes[0]];
                arranjos.Add(new Forma(melhor.Colunas, melhor.Topo, melhor.Base, partesCanonicas));
            }
        }

        TentarDirecao(mascarasA, mascarasB, baseEhA: true);
        TentarDirecao(mascarasB, mascarasA, baseEhA: false);

        if (arranjos.Count == 0) return null;

        var areaSolta = AreaDaMascara(mascarasA) + AreaDaMascara(mascarasB);
        var uteis = arranjos.Where(f => (double)f.Colunas * (f.MaxBase + 1) < areaSolta * FatorDeUtilidade).ToList();

        if (uteis.Count == 0) return null;

        var melhorAreaGeral = uteis.Min(f => (double)f.Colunas * (f.MaxBase + 1));
        return new ArranjoCruzado(uteis, areaSolta, melhorAreaGeral);
    }

    /// <summary>Área da peça na rotação de referência (qualquer uma — área não muda com rotação) — mesma conta que decide se um bloco compensa.</summary>
    private static double AreaDaMascara(IReadOnlyDictionary<int, Mascara> mascaras)
    {
        var m = mascaras.Values.First();
        return (double)m.Colunas * m.Linhas;
    }
}
