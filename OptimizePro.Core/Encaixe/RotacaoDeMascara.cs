namespace OptimizePro.Core.Encaixe;

/// <summary>Gira uma <see cref="Mascara"/> em múltiplos de 90° — usado para gerar as máscaras por rotação de um item (§11.1/§11.11).</summary>
public static class RotacaoDeMascara
{
    public static Mascara Rotacionar(Mascara origem, int grausMultiploDe90)
    {
        var g = ((grausMultiploDe90 % 360) + 360) % 360;

        return g switch
        {
            0 => origem,
            90 => Rotacionar90(origem),
            180 => Rotacionar90(Rotacionar90(origem)),
            270 => Rotacionar90(Rotacionar90(Rotacionar90(origem))),
            _ => throw new ArgumentException("Rotação precisa ser múltiplo de 90.", nameof(grausMultiploDe90)),
        };
    }

    /// <summary>Rotação de 90° (sentido fixo, só importa ser consistente — não a direção exata).</summary>
    private static Mascara Rotacionar90(Mascara origem)
    {
        var c0 = origem.Colunas;
        var l0 = origem.Linhas;

        var novoDesenho = new byte[l0 * c0];
        var novoCheio = new byte[l0 * c0];

        for (var c1 = 0; c1 < l0; c1++)
        {
            for (var l1 = 0; l1 < c0; l1++)
            {
                var cOrig = l1;
                var lOrig = l0 - 1 - c1;
                var idxOrig = cOrig * l0 + lOrig;
                var idxNovo = c1 * c0 + l1;

                novoDesenho[idxNovo] = origem.Desenho[idxOrig];
                novoCheio[idxNovo] = origem.Cheio[idxOrig];
            }
        }

        // offsets trocam de eixo junto com a rotação (aproximação — suficiente para o
        // encaixe; refinar se a UI precisar reancorar a imagem original com exatidão).
        return new Mascara(l0, c0, novoDesenho, novoCheio, origem.OffYCm, origem.OffXCm);
    }
}
