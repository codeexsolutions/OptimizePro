using OptimizePro.Core;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Services.Encaixe;

/// <summary>
/// Converte um contorno de peça (polígono em cm) para a <see cref="Mascara"/> rasterizada
/// que o encaixador de contorno consome — o passo "silhueta bruta" de §11.1/§11.2, aqui
/// via teste ponto-a-ponto (centro de célula dentro do polígono) em vez de rasterização de
/// imagem, já que a origem aqui é sempre um contorno vetorial (não uma imagem com canal alfa).
/// </summary>
internal static class RasterizacaoDePeca
{
    public static Mascara Rasterizar(IReadOnlyList<PontoXY> contorno, Grade grade)
    {
        var caixa = Geometria.CaixaDeContorno(contorno);
        var cols = Math.Max(1, (int)Math.Ceiling(caixa.Largura / grade.PassoCm));
        var linhas = Math.Max(1, (int)Math.Ceiling(caixa.Altura / grade.PassoCm));

        var silhueta = new bool[cols, linhas];
        for (var c = 0; c < cols; c++)
        {
            var x = caixa.MinX + (c + 0.5) * grade.PassoCm;
            for (var l = 0; l < linhas; l++)
            {
                var y = caixa.MinY + (l + 0.5) * grade.PassoCm;
                silhueta[c, l] = Geometria.PontoDentroDoPoligono(new PontoXY(x, y), contorno);
            }
        }

        return Mascara.DeSilhueta(silhueta, grade.Raio, caixa.MinX, caixa.MinY);
    }
}
