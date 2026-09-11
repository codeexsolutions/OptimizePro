using OptimizePro.Core;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace OptimizePro.Services.Encaixe;

/// <summary>
/// "PDF em tamanho real" (porte de <c>encaixe-pdf.js</c>, §11) — um arquivo só, com a largura
/// exata do tecido e o comprimento exato do encaixe, em centímetros de verdade: imprimindo em
/// escala 1:1, o que sai no papel mede o que a peça mede. Só o contorno de cada peça é
/// desenhado (linha de corte) — nada de régua, nome de peça ou rodapé, porque isso não devia
/// ir junto no tecido.
/// </summary>
/// <remarks>
/// Diferente do <c>encaixe-pdf.js</c> original: lá cada peça é uma ARTE (imagem de tecido
/// estampado) embutida no PDF; aqui uma peça é um <see cref="PontoXY"/>[] (contorno de molde,
/// vindo de DXF/PLT/SVG/PDF) — não existe imagem por peça neste porte, então o "desenho" é o
/// contorno mesmo, como linha de corte. A rotação usa exatamente a mesma conta de
/// <c>EncaixeViewModel.MontarGeometriaRotacionada</c> (mesmo algoritmo, aqui em pontos de PDF
/// em vez de pixels de tela) — os dois têm que concordar, senão o PDF sai diferente do que a
/// tela mostrou.
/// </remarks>
public static class ExportadorDeEncaixeParaPdf
{
    private const double PtPorCm = 72.0 / 2.54; // 1 ponto = 1/72 de polegada
    private const double LimitePt = 14400; // 508 cm — o maior lado que o PDF aceita numa página

    public static byte[] Exportar(
        double larguraTecidoCm, double consumoCm, IReadOnlyList<ItemDeResultado> posicoes,
        IReadOnlyDictionary<string, IReadOnlyList<PontoXY>> contornosPorPeca,
        double? comprimentoBancadaCm = null)
    {
        var paginas = PaginasDoEncaixe(posicoes, consumoCm, comprimentoBancadaCm);
        var larguraPt = larguraTecidoCm * PtPorCm;
        var maiorAlturaPt = paginas.Max(p => (p.Fundo - p.Topo) * PtPorCm);

        // Mesmo truque do /UserUnit do encaixe-pdf.js: um rolo de vários metros passa longe do
        // teto de 508cm do formato PDF. Em vez de dar erro ou cortar a página, a unidade da
        // página cresce só o necessário (arredondado pra cima, a página sobra um tiquinho em
        // vez de faltar) — os números dentro do PDF ficam dentro do limite, o tamanho real
        // impresso continua o mesmo. Uma unidade só pro documento inteiro (calculada pela maior
        // página) — não uma por página — pra duas páginas do mesmo rolo nunca saírem em escalas
        // diferentes por causa de arredondamento.
        var unidade = UnidadeDaPagina(larguraPt, maiorAlturaPt);

        using var documento = new PdfDocument();
        // /UserUnit é recurso do PDF 1.6 — o PdfSharp já escreve 1.7 por padrão (folga o
        // bastante), mas força explicitamente quando o recurso é usado, documentando a
        // dependência em vez de contar com o padrão da biblioteca continuar sendo >= 1.6 pra sempre.
        if (unidade != 1) documento.Version = 16;

        var caneta = new XPen(XColors.Black, 0.75);

        foreach (var pagina in paginas)
        {
            var paginaPdf = documento.AddPage();
            paginaPdf.Width = XUnit.FromPoint(larguraPt / unidade);
            paginaPdf.Height = XUnit.FromPoint((pagina.Fundo - pagina.Topo) * PtPorCm / unidade);
            if (unidade != 1) paginaPdf.Elements.SetReal("/UserUnit", unidade);

            using var graficos = XGraphics.FromPdfPage(paginaPdf);

            foreach (var posicao in pagina.Posicoes)
            {
                var grupo = posicao.PecaId.Split('#')[0];
                var contornoBase = contornosPorPeca.TryGetValue(grupo, out var contorno)
                    ? contorno
                    : RetanguloPadrao(posicao);

                // O Y da peça é medido no rolo inteiro; na página ele conta a partir do começo da bancada.
                var caminho = MontarCaminhoRotacionado(contornoBase, posicao.RotacaoGraus, posicao.X, posicao.Y - pagina.Topo, PtPorCm / unidade);
                graficos.DrawPath(caneta, caminho);
            }
        }

        using var ms = new MemoryStream();
        documento.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private sealed record PaginaDeEncaixe(double Topo, double Fundo, IReadOnlyList<ItemDeResultado> Posicoes);

    /// <summary>
    /// Reparte as posições em páginas: uma por bancada (porte de <c>paginasDoEncaixe</c> em
    /// <c>encaixe-pdf.js</c>). Sem bancada (ou uma peça só cabendo numa), sai uma página só com
    /// o consumo inteiro do rolo — igual ao comportamento de sempre. Como o motor de
    /// posicionamento (<see cref="OptimizePro.Core.Encaixe.Bancada"/>) já garante que nenhuma
    /// peça cruza a linha de uma bancada, <c>floor(Y / comprimento)</c> identifica corretamente
    /// a qual bancada cada peça pertence sem precisar de um número carimbado na posição.
    /// </summary>
    private static IReadOnlyList<PaginaDeEncaixe> PaginasDoEncaixe(IReadOnlyList<ItemDeResultado> posicoes, double consumoCm, double? comprimentoBancadaCm)
    {
        if (comprimentoBancadaCm is not (> 0) || posicoes.Count == 0)
            return [new PaginaDeEncaixe(0, consumoCm, posicoes)];

        var porBancada = posicoes
            .GroupBy(p => (int)Math.Floor(p.Y / comprimentoBancadaCm.Value))
            .OrderBy(g => g.Key)
            .Select(g => new PaginaDeEncaixe(g.Min(p => p.Y), g.Max(p => p.Y + p.AlturaCm), g.ToList()))
            .ToList();

        return porBancada.Count <= 1 ? [new PaginaDeEncaixe(0, consumoCm, posicoes)] : porBancada;
    }

    /// <summary>1 enquanto o rolo couber no teto do formato (a maioria dos encaixes cai nisso); passando do teto, cresce só o necessário.</summary>
    internal static double UnidadeDaPagina(double larguraPt, double alturaPt)
    {
        var maiorLado = Math.Max(larguraPt, alturaPt);
        if (maiorLado <= LimitePt) return 1;
        return Math.Ceiling(maiorLado / LimitePt * 100) / 100;
    }

    private static IReadOnlyList<PontoXY> RetanguloPadrao(ItemDeResultado posicao) =>
        [new(0, 0), new(posicao.LarguraCm, 0), new(posicao.LarguraCm, posicao.AlturaCm), new(0, posicao.AlturaCm)];

    /// <summary>Mesma conta de <c>EncaixeViewModel.MontarGeometriaRotacionada</c> — gira o contorno REAL da peça (não a caixa) e desloca pra posição final, aqui em pontos de PDF em vez de pixels de tela.</summary>
    private static XGraphicsPath MontarCaminhoRotacionado(IReadOnlyList<PontoXY> contorno, int rotacaoGraus, double posX, double posY, double escala)
    {
        PontoXY Girar(PontoXY p) => rotacaoGraus switch
        {
            90 => new PontoXY(-p.Y, p.X),
            180 => new PontoXY(-p.X, -p.Y),
            270 => new PontoXY(p.Y, -p.X),
            _ => p,
        };

        var girados = contorno.Select(Girar).ToList();
        var minX = girados.Min(p => p.X);
        var minY = girados.Min(p => p.Y);

        var caminho = new XGraphicsPath();
        caminho.AddLines(girados.Select(p => new XPoint((p.X - minX + posX) * escala, (p.Y - minY + posY) * escala)).ToArray());
        caminho.CloseFigure();
        return caminho;
    }
}
