namespace OptimizePro.Core;

/// <summary>
/// Porte de <c>public/geometria.js</c> — funções puras de geometria 2D usadas por
/// Moldes, Arte, Encaixe e Vetor. Sem dependência de UI/EF, testável isoladamente.
/// </summary>
/// <remarks>
/// Fica em <c>namespace OptimizePro.Core</c> (não <c>OptimizePro.Core.Geometria</c>)
/// de propósito: nomear o namespace igual à classe estática causa ambiguidade de
/// resolução no C# (o compilador prioriza o namespace sobre o tipo em chamadas
/// como <c>Geometria.AreaComSinal(...)</c>). A pasta <c>Geometria/</c> é só organização.
/// </remarks>
public static class Geometria
{
    public const double GeoEpsilon = 1e-9;

    /// <summary>Área com sinal (shoelace). Positiva = sentido anti-horário.</summary>
    public static double AreaComSinal(IReadOnlyList<PontoXY> pontos)
    {
        var n = pontos.Count;
        if (n < 3)
            return 0.0;

        var soma = 0.0;
        for (var i = 0; i < n; i++)
        {
            var atual = pontos[i];
            var proximo = pontos[(i + 1) % n];
            soma += atual.X * proximo.Y - proximo.X * atual.Y;
        }

        return soma / 2.0;
    }

    /// <summary>
    /// Gira um contorno em torno da origem, em múltiplos de 90° (§11.11/§11.5) — mesma
    /// convenção já usada na tela pra desenhar o resultado rotacionado
    /// (<c>EncaixeViewModel.MontarGeometriaRotacionada</c>), portada pra cá porque o motor NFP
    /// (que trabalha em contorno vetorial puro, não em máscara de grade) também precisa girar
    /// peça sem depender da camada de UI. Não normaliza a caixa delimitadora pro canto — quem
    /// chama recalcula via <see cref="CaixaDeContorno"/>, que já lida com qualquer deslocamento.
    /// </summary>
    public static IReadOnlyList<PontoXY> Rotacionar90(IReadOnlyList<PontoXY> pontos, int grausMultiplo90) => grausMultiplo90 switch
    {
        90 => [.. pontos.Select(p => new PontoXY(-p.Y, p.X))],
        180 => [.. pontos.Select(p => new PontoXY(-p.X, -p.Y))],
        270 => [.. pontos.Select(p => new PontoXY(p.Y, -p.X))],
        _ => pontos,
    };

    public static CaixaXY CaixaDeContorno(IReadOnlyList<PontoXY> pontos)
    {
        if (pontos.Count == 0)
            return new CaixaXY(0, 0, 0, 0);

        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        foreach (var p in pontos)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        return new CaixaXY(minX, minY, maxX, maxY);
    }

    public static double LadoMenorDoContorno(IReadOnlyList<PontoXY> pontos)
    {
        var caixa = CaixaDeContorno(pontos);
        return Math.Min(caixa.Largura, caixa.Altura);
    }

    public static double DistanciaEntre(PontoXY a, PontoXY b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Distância de <paramref name="p"/> ao segmento a-b, com a projeção clampada em [0,1].</summary>
    public static double DistanciaAteSegmento(PontoXY p, PontoXY a, PontoXY b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var comprimentoAoQuadrado = dx * dx + dy * dy;

        if (comprimentoAoQuadrado < GeoEpsilon)
            return DistanciaEntre(p, a);

        var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / comprimentoAoQuadrado;
        t = Math.Clamp(t, 0.0, 1.0);

        var projecao = new PontoXY(a.X + t * dx, a.Y + t * dy);
        return DistanciaEntre(p, projecao);
    }

    /// <summary>
    /// Douglas-Peucker iterativo (pilha explícita, sem recursão) — reduz o número de
    /// pontos de <paramref name="pontos"/> preservando a forma dentro de <paramref name="tolerancia"/>.
    /// </summary>
    public static IReadOnlyList<PontoXY> Simplificar(IReadOnlyList<PontoXY> pontos, double tolerancia)
    {
        var n = pontos.Count;
        if (n < 3 || tolerancia <= 0)
            return [.. pontos];

        var manter = new bool[n];
        manter[0] = true;
        manter[n - 1] = true;

        var pilha = new Stack<(int Inicio, int Fim)>();
        pilha.Push((0, n - 1));

        while (pilha.Count > 0)
        {
            var (inicio, fim) = pilha.Pop();
            if (fim - inicio < 2)
                continue;

            var a = pontos[inicio];
            var b = pontos[fim];

            var maiorDistancia = -1.0;
            var indiceMaisDistante = -1;

            for (var i = inicio + 1; i < fim; i++)
            {
                var distancia = DistanciaAteSegmento(pontos[i], a, b);
                if (distancia > maiorDistancia)
                {
                    maiorDistancia = distancia;
                    indiceMaisDistante = i;
                }
            }

            if (maiorDistancia > tolerancia && indiceMaisDistante >= 0)
            {
                manter[indiceMaisDistante] = true;
                pilha.Push((inicio, indiceMaisDistante));
                pilha.Push((indiceMaisDistante, fim));
            }
        }

        var resultado = new List<PontoXY>(n);
        for (var i = 0; i < n; i++)
        {
            if (manter[i])
                resultado.Add(pontos[i]);
        }

        return resultado;
    }

    /// <summary>
    /// Mesma simplificação de <see cref="Simplificar"/>, mas também devolve, pra cada ponto
    /// mantido, seu índice no array ORIGINAL — usado por quem precisa checar o ajuste de
    /// reta/arco contra o contorno bruto de verdade (ver <see cref="MontadorDeCaminho"/>),
    /// não só contra os poucos vértices que sobraram depois de simplificar.
    /// </summary>
    public static (IReadOnlyList<PontoXY> Pontos, IReadOnlyList<int> IndicesOriginais) SimplificarComIndices(IReadOnlyList<PontoXY> pontos, double tolerancia)
    {
        var n = pontos.Count;
        if (n < 3 || tolerancia <= 0)
            return ([.. pontos], Enumerable.Range(0, n).ToArray());

        var manter = new bool[n];
        manter[0] = true;
        manter[n - 1] = true;

        var pilha = new Stack<(int Inicio, int Fim)>();
        pilha.Push((0, n - 1));

        while (pilha.Count > 0)
        {
            var (inicio, fim) = pilha.Pop();
            if (fim - inicio < 2)
                continue;

            var a = pontos[inicio];
            var b = pontos[fim];

            var maiorDistancia = -1.0;
            var indiceMaisDistante = -1;

            for (var i = inicio + 1; i < fim; i++)
            {
                var distancia = DistanciaAteSegmento(pontos[i], a, b);
                if (distancia > maiorDistancia)
                {
                    maiorDistancia = distancia;
                    indiceMaisDistante = i;
                }
            }

            if (maiorDistancia > tolerancia && indiceMaisDistante >= 0)
            {
                manter[indiceMaisDistante] = true;
                pilha.Push((inicio, indiceMaisDistante));
                pilha.Push((indiceMaisDistante, fim));
            }
        }

        var pontosResultado = new List<PontoXY>(n);
        var indicesResultado = new List<int>(n);
        for (var i = 0; i < n; i++)
        {
            if (manter[i])
            {
                pontosResultado.Add(pontos[i]);
                indicesResultado.Add(i);
            }
        }

        return (pontosResultado, indicesResultado);
    }

    /// <summary>Teste ponto-em-polígono por ray-casting (não considera furos).</summary>
    public static bool PontoDentroDoPoligono(PontoXY p, IReadOnlyList<PontoXY> poligono)
    {
        var dentro = false;
        var n = poligono.Count;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = poligono[i];
            var pj = poligono[j];

            var cruzaFaixaY = (pi.Y > p.Y) != (pj.Y > p.Y);
            if (cruzaFaixaY)
            {
                var xDoCruzamento = (pj.X - pi.X) * (p.Y - pi.Y) / (pj.Y - pi.Y) + pi.X;
                if (p.X < xDoCruzamento)
                    dentro = !dentro;
            }
        }

        return dentro;
    }
}
