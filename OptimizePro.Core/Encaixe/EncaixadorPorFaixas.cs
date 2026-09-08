namespace OptimizePro.Core.Encaixe;

public sealed record ResultadoFaixas(IReadOnlyList<PosicaoDeItem> Posicoes, IReadOnlyList<string> ItensNaoEncaixados, int FundoMaximo);

/// <summary>
/// Porte do encaixador por faixas / strip packing (§11.6): divide o rolo em 2 colunas
/// verticais e roda o <see cref="EncaixadorPorContorno"/> independentemente em cada uma.
/// </summary>
/// <remarks>
/// A especificação também descreve a BUSCA pelo melhor corte (candidatos vêm das larguras
/// das próprias peças) e por qual item vai em qual faixa — isso já é decisão de "receita"
/// (§11.7, ainda não portado), não do mecanismo em si. Aqui recebe o corte e a partição
/// já decididos por quem chama; medido no sistema original como "perdedor" na maioria dos
/// casos (~5-7% pior que contorno), então nunca é a escolha padrão — só entra por
/// configuração explícita.
/// </remarks>
public static class EncaixadorPorFaixas
{
    public static ResultadoFaixas Encaixar(
        int colsTecido, int colunaDeCorte,
        IReadOnlyList<(ItemEncaixe Item, int RotacaoGraus)> ordemFaixa1,
        IReadOnlyList<(ItemEncaixe Item, int RotacaoGraus)> ordemFaixa2,
        HeuristicaDeContorno heuristica)
    {
        if (colunaDeCorte <= 0 || colunaDeCorte >= colsTecido)
            throw new ArgumentOutOfRangeException(nameof(colunaDeCorte), "A coluna de corte precisa ficar estritamente entre 0 e colsTecido.");

        var resultado1 = EncaixadorPorContorno.Encaixar(colunaDeCorte, ordemFaixa1, heuristica);
        var resultado2 = EncaixadorPorContorno.Encaixar(colsTecido - colunaDeCorte, ordemFaixa2, heuristica);

        List<PosicaoDeItem> posicoes =
        [
            .. resultado1.Posicoes,
            .. resultado2.Posicoes.Select(p => p with { X = p.X + colunaDeCorte }),
        ];

        List<string> naoEncaixados = [.. resultado1.ItensNaoEncaixados, .. resultado2.ItensNaoEncaixados];

        return new ResultadoFaixas(posicoes, naoEncaixados, Math.Max(resultado1.FundoMaximo, resultado2.FundoMaximo));
    }
}
