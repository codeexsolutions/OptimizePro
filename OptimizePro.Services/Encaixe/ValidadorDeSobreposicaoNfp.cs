using OptimizePro.Core;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Services.Encaixe;

/// <summary>
/// Porte da "regra de segurança" do guia de melhorias de aproveitamento (§7): "Nenhuma
/// posição NFP entra no resultado sem passar pela mesma validação discreta usada pelo motor
/// e pela bancada de sobreposição." O <see cref="OptimizePro.Core.Encaixe.Nfp.EncaixadorPorNfp"/>
/// já valida por polígono contínuo (§11.5) — mas é matemática de UM tipo só; o guia pede uma
/// segunda checagem, de natureza DIFERENTE (grade discreta, a mesma que já prova zero
/// sobreposição no motor de contorno há meses), como rede de segurança independente. Se as
/// duas discordarem, quem manda é a grade — nunca deixa uma posição NFP passar sem essa
/// confirmação.
/// </summary>
internal static class ValidadorDeSobreposicaoNfp
{
    public static bool SemSobreposicao(IReadOnlyList<(IReadOnlyList<PontoXY> Contorno, double X, double Y)> posicionados, Grade grade)
    {
        if (posicionados.Count <= 1) return true;

        var caixas = posicionados.Select(p => (p.X, p.Y, Caixa: Geometria.CaixaDeContorno(p.Contorno))).ToList();
        var minX = caixas.Min(p => p.X + p.Caixa.MinX);
        var maxX = caixas.Max(p => p.X + p.Caixa.MaxX);
        var minY = caixas.Min(p => p.Y + p.Caixa.MinY);
        var maxY = caixas.Max(p => p.Y + p.Caixa.MaxY);

        var colsTotais = Math.Max(1, (int)Math.Ceiling((maxX - minX) / grade.PassoCm)) + 2;
        var linhasTotais = Math.Max(1, (int)Math.Ceiling((maxY - minY) / grade.PassoCm)) + 2;

        // Tecido real pode passar de várias centenas de milhares de células — bitset, não bool[,].
        var ocupado = new System.Collections.BitArray(colsTotais * linhasTotais);

        foreach (var (contorno, x, y) in posicionados)
        {
            var mascara = RasterizacaoDePeca.Rasterizar(contorno, grade);
            var caixa = Geometria.CaixaDeContorno(contorno);

            var origemCol = (int)Math.Round((x + caixa.MinX - minX) / grade.PassoCm);
            var origemLinha = (int)Math.Round((y + caixa.MinY - minY) / grade.PassoCm);

            for (var c = 0; c < mascara.Colunas; c++)
            {
                var col = origemCol + c;
                if (col < 0 || col >= colsTotais) continue;

                for (var l = 0; l < mascara.Linhas; l++)
                {
                    if (mascara.ObterCheio(c, l) == 0) continue;

                    var linha = origemLinha + l;
                    if (linha < 0 || linha >= linhasTotais) continue;

                    var indice = col * linhasTotais + linha;
                    if (ocupado[indice]) return false; // colisão real, achada por um cálculo independente do NFP.
                    ocupado[indice] = true;
                }
            }
        }

        return true;
    }
}
