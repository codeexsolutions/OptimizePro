using Clipper2Lib;

namespace OptimizePro.Core.Encaixe.Nfp;

/// <summary>
/// NFP (No-Fit Polygon) entre dois contornos, via soma de Minkowski (§11.5).
/// </summary>
/// <remarks>
/// A especificação original decompõe cada peça em polígonos convexos (Hertel-Mehlhorn)
/// porque a soma de Minkowski "manual" só é simples entre convexos. Usa-se aqui o
/// <c>Clipper2</c> (NuGet, licença Boost — grátis para uso comercial) em vez disso: sua
/// <see cref="Clipper.MinkowskiSum"/> já soma segmento a segmento e funciona direto em
/// polígonos côncavos — só precisa unir o resultado (pode sair fragmentado em mais de um
/// polígono) com <see cref="Clipper.Union(PathsD, FillRule)"/>. Evita a decomposição
/// convexa inteira (passos 4-5 de "pecaEmPoligonos", §11.5) e a aproximação por casco
/// convexo para peças com mais de 16 pedaços.
/// </remarks>
public static class CalculadoraDeNfp
{
    /// <summary>
    /// NFP(<paramref name="paradaA"/>, <paramref name="movelB"/>): fronteira das posições
    /// onde a origem local (0,0) de B pode ficar tocando A sem invadir — <c>nfpConvexo</c>
    /// da especificação, generalizado para côncavos via Clipper2.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<PontoXY>> Calcular(IReadOnlyList<PontoXY> paradaA, IReadOnlyList<PontoXY> movelB)
    {
        var a = ComecarNoCantoDeBaixo(GarantirAntiHorario(ParaPathD(paradaA)));

        var bInvertidoBruto = new PathD(ParaPathD(movelB).Select(p => new PointD(-p.x, -p.y)));
        var bInvertido = ComecarNoCantoDeBaixo(GarantirAntiHorario(bInvertidoBruto));

        var somas = Clipper.MinkowskiSum(a, bInvertido, isClosed: true);
        var unido = Clipper.Union(somas, FillRule.NonZero);

        // O Clipper2 pode devolver fragmentos de área negativa (sentido horário) junto do
        // resultado — não é um "buraco" de verdade (soma de Minkowski de dois polígonos
        // é sempre uma região preenchida, nunca com furo), é ruído do cálculo bruto antes
        // da união. Mantém só as regiões de área positiva (reproduzido em teste: um
        // quadrado 2x2 com um 1x1 sai com um fragmento espúrio de área -1 além do
        // resultado correto de área +9).
        return [.. unido.Where(Clipper.IsPositive).Select(ParaPontosXY)];
    }

    private static PathD ParaPathD(IReadOnlyList<PontoXY> pontos) => new(pontos.Select(p => new PointD(p.X, p.Y)));

    private static IReadOnlyList<PontoXY> ParaPontosXY(PathD caminho) => [.. caminho.Select(p => new PontoXY(p.x, p.y))];

    private static PathD GarantirAntiHorario(PathD caminho) =>
        Clipper.IsPositive(caminho) ? caminho : new PathD(Enumerable.Reverse(caminho));

    /// <summary>
    /// Gira a lista de vértices para começar no "canto de baixo" (§11.5: "percorre lados
    /// ordenados a partir de cantoDeBaixo(a)+cantoDeBaixo(b)") — sem isso, o Clipper2 pode
    /// devolver a soma de Minkowski fragmentada em pedaços espúrios dependendo de qual
    /// vértice o polígono de entrada começa (mesma forma, mesmo sentido, resultado
    /// diferente — reproduzido em teste).
    /// </summary>
    private static PathD ComecarNoCantoDeBaixo(PathD caminho)
    {
        var indiceMinimo = 0;
        for (var i = 1; i < caminho.Count; i++)
        {
            if (caminho[i].y < caminho[indiceMinimo].y || (caminho[i].y == caminho[indiceMinimo].y && caminho[i].x < caminho[indiceMinimo].x))
                indiceMinimo = i;
        }

        if (indiceMinimo == 0)
            return caminho;

        var rotacionado = new PathD(caminho.Count);
        for (var i = 0; i < caminho.Count; i++)
            rotacionado.Add(caminho[(indiceMinimo + i) % caminho.Count]);

        return rotacionado;
    }
}
