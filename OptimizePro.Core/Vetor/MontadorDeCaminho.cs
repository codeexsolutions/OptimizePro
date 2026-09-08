namespace OptimizePro.Core.Vetor;

/// <summary>
/// Porte de <c>caminhoDoContorno</c> (§14.1 passo 8, §14.6) — para cada trecho entre
/// cantos vivos, testa até onde vai uma reta e até onde vai um arco (ajuste de Kåsa); o
/// arco só vence se alcançar significativamente mais longe. Senão, gera Bézier cúbica
/// usando as tangentes já calculadas.
/// </summary>
/// <remarks>
/// A especificação descreve a decisão reta/arco/Bézier em prosa, sem pseudocódigo exato
/// (ao contrário do ajuste de Kåsa, que tinha fórmula). Interpretação adotada aqui,
/// documentada por ser uma escolha de projeto e não algo extraído literalmente do texto:
/// um avanço de reta de 1 único passo é o caso normal pra uma aresta comum (já veio
/// simplificada por Douglas-Peucker antes deste passo) — só vira Bézier quando esse
/// passo NÃO termina numa quina de verdade (aí sim é um trecho curto de uma curva
/// suave); senão emite reta. Arco vence se alcançar consideravelmente mais longe que a
/// reta (margem ADITIVA, não multiplicativa — ver <see cref="MargemMinimaDoArco"/>) E os
/// pontos do trecho estiverem densos o bastante angularmente (ver
/// <see cref="LimiarDePassoAngularGraus"/> — sem essa peneira, poucos pontos esparsos que
/// coincidentemente caem sobre um círculo, como os 4 cantos de um retângulo — retângulos
/// são cíclicos! — viram um "arco" errado que ignora as arestas retas entre eles).
///
/// <para>
/// <b>Checagem contra o contorno BRUTO (02/09/2026, achado comparando com a referência):</b>
/// quando <paramref name="bruto"/>/<paramref name="indicesNoBruto"/> são passados, o ajuste de
/// reta/arco é verificado contra os pontos do contorno ORIGINAL (antes de Douglas-Peucker)
/// entre as duas âncoras — não contra os próprios vértices já simplificados. Sem isso, checar
/// o ajuste contra só os 2-3 vértices que sobraram da simplificação é quase tautológico (um
/// círculo sempre "ajusta" perfeitamente a 2-3 pontos quaisquer que não sejam colineares) —
/// um recorte côncavo (o garfo de um "Y", por exemplo) que a simplificação reduziu a poucos
/// pontos passava no teste de arco e virava uma curva lisa, apagando o recorte de verdade.
/// Sem <paramref name="bruto"/> (compatibilidade com chamadores que só têm o polígono já
/// simplificado), cai de volta pro comportamento antigo — mais rápido, mas sujeito a esse
/// problema em formas com recortes finos.
/// </para>
/// </remarks>
/// <example>
/// Com o "quina" padrão (55°), um canto de 90° NÃO é detectado como canto vivo (180-90=90,
/// que não fica abaixo de 55) — um retângulo perfeito sai com os 4 cantos suavizados em
/// Bézier, não com linhas retas. Isso é consistente com o propósito da ferramenta (traçar
/// arte fotografada, onde cantos "perfeitos" são raros) — se o resultado esperado for
/// cantos vivos de verdade, aumentar "quina" (validado até 100° nos testes).
/// </example>
public static class MontadorDeCaminho
{
    public static CaminhoMontado Montar(
        IReadOnlyList<PontoXY> contornoOriginal, double quinaGraus, ParametrosDeRemontagem parametros,
        IReadOnlyList<PontoXY>? bruto = null, IReadOnlyList<int>? indicesNoBruto = null)
    {
        var fechado = contornoOriginal.Count >= 2 && contornoOriginal[0] == contornoOriginal[^1];
        var contorno = RemoverFechamentoDuplicado(contornoOriginal);
        var indices = fechado && indicesNoBruto is not null ? indicesNoBruto.Take(indicesNoBruto.Count - 1).ToList() : indicesNoBruto;
        var n = contorno.Count;

        if (n < 3)
            return new CaminhoMontado(n > 0 ? contorno[0] : default, n == 2 ? [new SegmentoReta(contorno[1])] : []);

        var quinas = DetectorDeQuinas.AcharQuinas(contorno, quinaGraus);
        var tangentes = TangentesDoContorno.Calcular(contorno, quinas);

        var segmentos = new List<SegmentoDeCaminho>();
        var i = 0;
        var passosAndados = 0;

        while (passosAndados < n)
        {
            var maxAvanco = Math.Min(ProximoLimiteRelativo(quinas, i, n), n - passosAndados);

            var avancoReta = EstenderReta(contorno, bruto, indices, i, maxAvanco, parametros.ToleranciaDeReta);
            var avancoArco = EstenderArco(contorno, bruto, indices, i, maxAvanco, parametros.ToleranciaDeArco);

            // Depois de simplificar (Douglas-Peucker, já aplicado antes deste passo), uma
            // reta de 1 único avanço é o caso NORMAL para uma aresta reta comum — não é
            // "falta de progresso". Só cai pra Bézier quando o avanço de 1 passo NÃO
            // termina numa quina de verdade (aí sim é um trecho curto fazendo parte de
            // uma curva suave, não um canto reto).
            if (avancoArco >= 2 && avancoArco >= avancoReta + MargemMinimaDoArco(avancoReta))
            {
                var trecho = ObterTrechoBrutoOuSimplificado(contorno, bruto, indices, i, avancoArco, n);
                var circulo = AjusteDeCirculo.Ajustar(trecho)!.Value;
                var meio = trecho[trecho.Count / 2];
                var fim = contorno[(i + avancoArco) % n];

                var (grande, horario) = DeterminarFlags(circulo.Centro, contorno[i], meio, fim);
                segmentos.Add(new SegmentoArco(circulo.Raio, grande, horario, fim));

                i = (i + avancoArco) % n;
                passosAndados += avancoArco;
            }
            else if (avancoReta >= 2 || quinas[(i + Math.Max(avancoReta, 1)) % n])
            {
                var avanco = Math.Max(avancoReta, 1);
                segmentos.Add(new SegmentoReta(contorno[(i + avanco) % n]));
                i = (i + avanco) % n;
                passosAndados += avanco;
            }
            else
            {
                var proximo = (i + 1) % n;
                var (c1, c2) = ControlesDeBezier(contorno[i], contorno[proximo], tangentes[i].Saida, tangentes[proximo].Entrada, parametros.TensaoDeBezier);
                segmentos.Add(new SegmentoBezier(c1, c2, contorno[proximo]));

                i = proximo;
                passosAndados += 1;
            }
        }

        return new CaminhoMontado(contorno[0], segmentos);
    }

    /// <summary>
    /// Margem mínima (em passos) que o arco precisa alcançar A MAIS que a reta pra vencer —
    /// ADITIVA (pelo menos +2, ou +25% do avanço da reta se isso for maior), não multiplicativa.
    /// Um fator multiplicativo (ex.: 1.2×) exige só +1 passo pra avanços curtos (típico bem
    /// onde recortes/notches vivem, depois de simplificados a poucos pontos) — margem baixa
    /// demais pra distinguir "curva de verdade" de "coincidência de poucos pontos".
    /// </summary>
    private static int MargemMinimaDoArco(int avancoReta) => Math.Max(2, (int)Math.Ceiling(avancoReta * 0.25));

    private static List<PontoXY> RemoverFechamentoDuplicado(IReadOnlyList<PontoXY> contorno) =>
        contorno.Count >= 2 && contorno[0] == contorno[^1] ? [.. contorno.Take(contorno.Count - 1)] : [.. contorno];

    /// <summary>Quantos passos dá pra avançar a partir de <paramref name="inicio"/> antes da próxima quina (nunca atravessa um canto vivo).</summary>
    private static int ProximoLimiteRelativo(IReadOnlyList<bool> quinas, int inicio, int n)
    {
        for (var avanco = 1; avanco < n; avanco++)
        {
            if (quinas[(inicio + avanco) % n])
                return avanco;
        }
        return n;
    }

    private static int EstenderReta(IReadOnlyList<PontoXY> contorno, IReadOnlyList<PontoXY>? bruto, IReadOnlyList<int>? indices, int inicio, int maxAvanco, double tolerancia)
    {
        var melhor = 0;
        for (var avanco = 1; avanco <= maxAvanco; avanco++)
        {
            if (!CabeNumaReta(contorno, bruto, indices, inicio, avanco, tolerancia))
                break;
            melhor = avanco;
        }
        return melhor;
    }

    private static bool CabeNumaReta(IReadOnlyList<PontoXY> contorno, IReadOnlyList<PontoXY>? bruto, IReadOnlyList<int>? indices, int inicio, int avanco, double tolerancia)
    {
        var n = contorno.Count;
        var a = contorno[inicio];
        var b = contorno[(inicio + avanco) % n];

        var amostras = ObterTrechoBrutoOuSimplificado(contorno, bruto, indices, inicio, avanco, n);
        foreach (var p in amostras)
        {
            if (Geometria.DistanciaAteSegmento(p, a, b) > tolerancia)
                return false;
        }

        return true;
    }

    /// <summary>Passo angular médio (graus) acima do qual um "arco" não é confiável — pontos esparsos demais podem cair sobre um círculo por coincidência (ex.: os 4 cantos de um retângulo, que é cíclico) sem que o trecho de fato siga uma curva.</summary>
    private const double LimiarDePassoAngularGraus = 45.0;

    private static int EstenderArco(IReadOnlyList<PontoXY> contorno, IReadOnlyList<PontoXY>? bruto, IReadOnlyList<int>? indices, int inicio, int maxAvanco, double tolerancia)
    {
        var melhor = 0;
        var n = contorno.Count;
        for (var avanco = 2; avanco <= maxAvanco; avanco++)
        {
            var trecho = ObterTrechoBrutoOuSimplificado(contorno, bruto, indices, inicio, avanco, n);
            var ajuste = AjusteDeCirculo.Ajustar(trecho);
            if (ajuste is not { } circulo)
                break;

            var erroMaximo = trecho.Max(p => Math.Abs(Geometria.DistanciaEntre(p, circulo.Centro) - circulo.Raio));
            if (erroMaximo > tolerancia)
                break;

            if (PassoAngularMedio(trecho, circulo.Centro) > LimiarDePassoAngularGraus)
                break;

            melhor = avanco;
        }
        return melhor;
    }

    private static double PassoAngularMedio(List<PontoXY> trecho, PontoXY centro)
    {
        if (trecho.Count < 2)
            return 0;

        double Angulo(PontoXY p) => Math.Atan2(p.Y - centro.Y, p.X - centro.X);

        var somaAbsoluta = 0.0;
        for (var k = 1; k < trecho.Count; k++)
        {
            var diferenca = Angulo(trecho[k]) - Angulo(trecho[k - 1]);
            while (diferenca > Math.PI) diferenca -= 2 * Math.PI;
            while (diferenca < -Math.PI) diferenca += 2 * Math.PI;
            somaAbsoluta += Math.Abs(diferenca);
        }

        return somaAbsoluta / (trecho.Count - 1) * 180.0 / Math.PI;
    }

    private static List<PontoXY> ObterTrecho(IReadOnlyList<PontoXY> contorno, int inicio, int avanco)
    {
        var n = contorno.Count;
        var trecho = new List<PontoXY>(avanco + 1);
        for (var k = 0; k <= avanco; k++)
            trecho.Add(contorno[(inicio + k) % n]);
        return trecho;
    }

    /// <summary>
    /// Quando o contorno bruto está disponível, amostra os pontos ORIGINAIS entre as duas
    /// âncoras (índices simplificados <paramref name="inicio"/> e <paramref name="inicio"/>+
    /// <paramref name="avanco"/>) — não só os vértices já simplificados, que podem ser poucos
    /// (ou nenhum) demais pra revelar um recorte côncavo real. Sem o bruto, cai pro
    /// comportamento antigo (só os vértices simplificados).
    /// </summary>
    private const int MaximoDeAmostrasDoBruto = 40;

    private static List<PontoXY> ObterTrechoBrutoOuSimplificado(
        IReadOnlyList<PontoXY> contorno, IReadOnlyList<PontoXY>? bruto, IReadOnlyList<int>? indices, int inicio, int avanco, int n)
    {
        if (bruto is null || indices is null || bruto.Count < 2)
            return ObterTrecho(contorno, inicio, avanco);

        var nB = bruto.Count;
        var ri = indices[inicio];
        var rj = indices[(inicio + avanco) % n];
        var passos = ((rj - ri) % nB + nB) % nB;

        var amostras = new List<PontoXY>(Math.Min(passos + 1, MaximoDeAmostrasDoBruto));
        if (passos + 1 <= MaximoDeAmostrasDoBruto)
        {
            for (var k = 0; k <= passos; k++)
                amostras.Add(bruto[(ri + k) % nB]);
        }
        else
        {
            for (var s = 0; s < MaximoDeAmostrasDoBruto; s++)
            {
                var k = (int)Math.Round(s * (double)passos / (MaximoDeAmostrasDoBruto - 1));
                amostras.Add(bruto[(ri + k) % nB]);
            }
        }

        return amostras;
    }

    /// <summary>
    /// Descobre o sentido real do percurso (horário/anti-horário, em coordenadas com Y
    /// para baixo — convenção SVG) usando um ponto do meio do trecho, e se a varredura
    /// passa de 180°. Mais simples que a "fórmula delicada" da especificação (testar as 4
    /// combinações de flag) porque já se tem o centro do círculo ajustado — não precisa
    /// redescobri-lo a partir só do raio.
    /// </summary>
    private static (bool GrandeArco, bool Horario) DeterminarFlags(PontoXY centro, PontoXY inicio, PontoXY meio, PontoXY fim)
    {
        double Angulo(PontoXY p) => Math.Atan2(p.Y - centro.Y, p.X - centro.X);

        double DiferencaAntiHoraria(double de, double para)
        {
            var d = para - de;
            while (d < 0) d += 2 * Math.PI;
            while (d >= 2 * Math.PI) d -= 2 * Math.PI;
            return d;
        }

        var angInicio = Angulo(inicio);
        var angMeio = Angulo(meio);
        var angFim = Angulo(fim);

        var varreduraAntiHoraria = DiferencaAntiHoraria(angInicio, angFim);
        var meioNaVarreduraAntiHoraria = DiferencaAntiHoraria(angInicio, angMeio) <= varreduraAntiHoraria;

        var horario = !meioNaVarreduraAntiHoraria;
        var varreduraReal = horario ? 2 * Math.PI - varreduraAntiHoraria : varreduraAntiHoraria;

        return (varreduraReal > Math.PI, horario);
    }

    private static (PontoXY C1, PontoXY C2) ControlesDeBezier(PontoXY p0, PontoXY p1, PontoXY tangenteSaida, PontoXY tangenteEntrada, double tensao)
    {
        var comprimentoDaAlca = tensao * Geometria.DistanciaEntre(p0, p1) / 3.0;

        var c1 = new PontoXY(p0.X + tangenteSaida.X * comprimentoDaAlca, p0.Y + tangenteSaida.Y * comprimentoDaAlca);
        var c2 = new PontoXY(p1.X - tangenteEntrada.X * comprimentoDaAlca, p1.Y - tangenteEntrada.Y * comprimentoDaAlca);

        return (c1, c2);
    }
}
