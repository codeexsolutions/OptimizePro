using OptimizePro.Core.Moldes;

namespace OptimizePro.Core.Moldes.Dxf;

/// <summary>
/// Converte a árvore de <see cref="DxfEntidadeBruta"/> em <see cref="Traco"/>/<see cref="RotuloTexto"/>,
/// aplicando a transformação acumulada de INSERTs aninhados (§8.1: profundidade máxima 8).
/// Coordenadas resultantes ainda estão na unidade nativa do arquivo (sem inversão de Y
/// nem conversão para cm — isso é feito pelo <see cref="LeitorDxf"/>).
/// </summary>
internal sealed class DxfConversor(IReadOnlyDictionary<string, DxfEntidadeBruta> blocos, List<string> avisos)
{
    private const int ProfundidadeMaximaDeInsert = 8;

    public void Converter(DxfEntidadeBruta entidade, Transformacao2D transformAtual, int profundidade, List<Traco> tracos, List<RotuloTexto> textos)
    {
        switch (entidade.Tipo)
        {
            case "LINE":
                ConverterLine(entidade, transformAtual, tracos);
                break;
            case "LWPOLYLINE":
                ConverterLwpolyline(entidade, transformAtual, tracos);
                break;
            case "POLYLINE":
                ConverterPolyline(entidade, transformAtual, tracos);
                break;
            case "CIRCLE":
                ConverterCircle(entidade, transformAtual, tracos);
                break;
            case "ARC":
                ConverterArc(entidade, transformAtual, tracos);
                break;
            case "ELLIPSE":
                ConverterEllipse(entidade, transformAtual, tracos);
                break;
            case "SPLINE":
                ConverterSpline(entidade, transformAtual, tracos);
                break;
            case "INSERT":
                ConverterInsert(entidade, transformAtual, profundidade, tracos, textos);
                break;
            case "TEXT":
            case "ATTRIB":
                ConverterTexto(entidade.Primeiro(1), entidade, transformAtual, textos);
                break;
            case "MTEXT":
                ConverterTexto(string.Concat(entidade.Todos(3)) + (entidade.Primeiro(1) ?? ""), entidade, transformAtual, textos);
                break;
        }
    }

    private static void ConverterLine(DxfEntidadeBruta entidade, Transformacao2D transform, List<Traco> tracos)
    {
        var p1 = transform.Aplicar(new PontoXY(entidade.PrimeiroDouble(10), entidade.PrimeiroDouble(20)));
        var p2 = transform.Aplicar(new PontoXY(entidade.PrimeiroDouble(11), entidade.PrimeiroDouble(21)));
        tracos.Add(new Traco([p1, p2], false));
    }

    private static void ConverterLwpolyline(DxfEntidadeBruta entidade, Transformacao2D transform, List<Traco> tracos)
    {
        var vertices = ExtrairVerticesComBulge(entidade.Grupos);
        if (vertices.Count < 2)
            return;

        var fechada = (entidade.PrimeiroInt(70) & 1) != 0;
        var pontos = ConstruirPontosDePolilinha(vertices, fechada);
        tracos.Add(new Traco([.. pontos.Select(transform.Aplicar)], fechada));
    }

    private static void ConverterPolyline(DxfEntidadeBruta entidade, Transformacao2D transform, List<Traco> tracos)
    {
        var vertices = entidade.Filhos
            .Select(v => (Ponto: new PontoXY(v.PrimeiroDouble(10), v.PrimeiroDouble(20)), Bulge: v.PrimeiroDouble(42)))
            .ToList();

        if (vertices.Count < 2)
            return;

        var fechada = (entidade.PrimeiroInt(70) & 1) != 0;
        var pontos = ConstruirPontosDePolilinha(vertices, fechada);
        tracos.Add(new Traco([.. pontos.Select(transform.Aplicar)], fechada));
    }

    private static void ConverterCircle(DxfEntidadeBruta entidade, Transformacao2D transform, List<Traco> tracos)
    {
        var centro = new PontoXY(entidade.PrimeiroDouble(10), entidade.PrimeiroDouble(20));
        var raio = entidade.PrimeiroDouble(40);
        var pontos = DxfCurvas.PontosDoArco(centro, raio, 0, 360);
        tracos.Add(new Traco([.. pontos.Select(transform.Aplicar)], true));
    }

    private static void ConverterArc(DxfEntidadeBruta entidade, Transformacao2D transform, List<Traco> tracos)
    {
        var centro = new PontoXY(entidade.PrimeiroDouble(10), entidade.PrimeiroDouble(20));
        var raio = entidade.PrimeiroDouble(40);
        var anguloInicial = entidade.PrimeiroDouble(50);
        var anguloFinal = entidade.PrimeiroDouble(51);
        var pontos = DxfCurvas.PontosDoArco(centro, raio, anguloInicial, anguloFinal);
        tracos.Add(new Traco([.. pontos.Select(transform.Aplicar)], false));
    }

    private static void ConverterEllipse(DxfEntidadeBruta entidade, Transformacao2D transform, List<Traco> tracos)
    {
        var cx = entidade.PrimeiroDouble(10);
        var cy = entidade.PrimeiroDouble(20);
        var ex = entidade.PrimeiroDouble(11);
        var ey = entidade.PrimeiroDouble(21);
        var razao = entidade.PrimeiroDouble(40, 1);
        var paramInicial = entidade.PrimeiroDouble(41);
        var paramFinal = entidade.Primeiro(42) is not null ? entidade.PrimeiroDouble(42) : 2 * Math.PI;

        var pontos = DxfCurvas.PontosDaElipse(cx, cy, ex, ey, razao, paramInicial, paramFinal);
        var fechada = paramFinal - paramInicial >= 2 * Math.PI - 1e-6;
        tracos.Add(new Traco([.. pontos.Select(transform.Aplicar)], fechada));
    }

    private void ConverterSpline(DxfEntidadeBruta entidade, Transformacao2D transform, List<Traco> tracos)
    {
        var grau = entidade.PrimeiroInt(71, 3);
        var nos = entidade.Todos(40).Select(ParseDoubleInvariante).ToList();
        var controle = ExtrairParesXY(entidade.Grupos, 10, 20);

        if (controle.Count < 2)
            return;

        if (grau < 1) grau = 1;
        var n = controle.Count - 1;

        if (nos.Count < n + grau + 2)
        {
            avisos.Add("SPLINE com nós insuficientes/malformados — usado o polígono de controle como aproximação.");
            var fechadaAprox = (entidade.PrimeiroInt(70) & 1) != 0;
            tracos.Add(new Traco([.. controle.Select(transform.Aplicar)], fechadaAprox));
            return;
        }

        var pontos = AmostrarSpline(grau, nos, controle, n);
        var fechada = (entidade.PrimeiroInt(70) & 1) != 0;
        tracos.Add(new Traco([.. pontos.Select(transform.Aplicar)], fechada));
    }

    private static IReadOnlyList<PontoXY> AmostrarSpline(int grau, List<double> nos, List<PontoXY> controle, int n)
    {
        var tInicial = nos[grau];
        var tFinal = nos[n + 1];
        var numSegmentos = Math.Max(20, controle.Count * 8);

        var pontos = new List<PontoXY>(numSegmentos + 1);
        for (var i = 0; i <= numSegmentos; i++)
        {
            var t = tInicial + (tFinal - tInicial) * i / numSegmentos;
            pontos.Add(DxfCurvas.AvaliarBSpline(grau, nos, controle, t));
        }

        return pontos;
    }

    private void ConverterInsert(DxfEntidadeBruta entidade, Transformacao2D transformAtual, int profundidade, List<Traco> tracos, List<RotuloTexto> textos)
    {
        var nomeBloco = entidade.Primeiro(2) ?? "";

        if (!blocos.TryGetValue(nomeBloco, out var bloco))
        {
            avisos.Add($"INSERT referencia bloco '{nomeBloco}' não encontrado — ignorado.");
            return;
        }

        if (profundidade >= ProfundidadeMaximaDeInsert)
        {
            avisos.Add($"Profundidade máxima de INSERT ({ProfundidadeMaximaDeInsert}) excedida em '{nomeBloco}' — ignorado.");
            return;
        }

        var insX = entidade.PrimeiroDouble(10);
        var insY = entidade.PrimeiroDouble(20);
        var escalaX = entidade.PrimeiroDouble(41, 1);
        var escalaY = entidade.PrimeiroDouble(42, 1);
        var rotacaoRad = entidade.PrimeiroDouble(50) * Math.PI / 180.0;

        var basePontoX = bloco.PrimeiroDouble(10);
        var basePontoY = bloco.PrimeiroDouble(20);

        var transformDoInsert = Transformacao2D.DeEscalaRotacaoTranslacao(escalaX, escalaY, rotacaoRad, insX, insY);
        var transformDoOffsetDeBase = Transformacao2D.DeEscalaRotacaoTranslacao(1, 1, 0, -basePontoX, -basePontoY);
        var transformLocal = transformDoInsert.ComposicaoCom(transformDoOffsetDeBase);
        var transformFinal = transformAtual.ComposicaoCom(transformLocal);

        foreach (var filho in bloco.Filhos)
            Converter(filho, transformFinal, profundidade + 1, tracos, textos);
    }

    private static void ConverterTexto(string? texto, DxfEntidadeBruta entidade, Transformacao2D transform, List<RotuloTexto> textos)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return;

        var p = transform.Aplicar(new PontoXY(entidade.PrimeiroDouble(10), entidade.PrimeiroDouble(20)));
        textos.Add(new RotuloTexto(texto, p.X, p.Y));
    }

    private static List<(PontoXY Ponto, double Bulge)> ExtrairVerticesComBulge(List<DxfPar> grupos)
    {
        var vertices = new List<(PontoXY Ponto, double Bulge)>();
        double x = 0, y = 0, bulge = 0;
        var iniciado = false;

        foreach (var grupo in grupos)
        {
            switch (grupo.Codigo)
            {
                case 10:
                    if (iniciado)
                        vertices.Add((new PontoXY(x, y), bulge));
                    x = ParseDoubleInvariante(grupo.Valor);
                    y = 0;
                    bulge = 0;
                    iniciado = true;
                    break;
                case 20:
                    y = ParseDoubleInvariante(grupo.Valor);
                    break;
                case 42:
                    bulge = ParseDoubleInvariante(grupo.Valor);
                    break;
            }
        }

        if (iniciado)
            vertices.Add((new PontoXY(x, y), bulge));

        return vertices;
    }

    private static List<PontoXY> ExtrairParesXY(List<DxfPar> grupos, int codigoX, int codigoY)
    {
        var pontos = new List<PontoXY>();
        double? x = null;

        foreach (var grupo in grupos)
        {
            if (grupo.Codigo == codigoX)
            {
                x = ParseDoubleInvariante(grupo.Valor);
            }
            else if (grupo.Codigo == codigoY && x is not null)
            {
                pontos.Add(new PontoXY(x.Value, ParseDoubleInvariante(grupo.Valor)));
                x = null;
            }
        }

        return pontos;
    }

    private static List<PontoXY> ConstruirPontosDePolilinha(List<(PontoXY Ponto, double Bulge)> vertices, bool fechada)
    {
        var pontos = new List<PontoXY> { vertices[0].Ponto };
        var quantidadeSegmentos = fechada ? vertices.Count : vertices.Count - 1;

        for (var i = 0; i < quantidadeSegmentos; i++)
        {
            var atual = vertices[i];
            var proximo = vertices[(i + 1) % vertices.Count];

            var trecho = Math.Abs(atual.Bulge) > 1e-12
                ? DxfCurvas.PontosDoArcoPorBulge(atual.Ponto, proximo.Ponto, atual.Bulge)
                : (IReadOnlyList<PontoXY>)[proximo.Ponto];

            pontos.AddRange(trecho);
        }

        return pontos;
    }

    private static double ParseDoubleInvariante(string valor) =>
        double.TryParse(valor, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
}
