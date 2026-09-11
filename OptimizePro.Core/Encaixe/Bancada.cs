namespace OptimizePro.Core.Encaixe;

/// <summary>
/// "A bancada" (porte de <c>encaixeMotor.js</c>, § "NÃO SE MEXE AQUI SEM A BANCADA") — o rolo
/// deixa de ser uma tira sem fim e passa a ser uma fila de bancadas do comprimento da mesa de
/// corte. Cabe numa regra só: NENHUMA PEÇA CRUZA A LINHA ENTRE DUAS BANCADAS. Quando a peça não
/// cabe no que sobrou da bancada atual, o único lugar onde ela cabe é a seguinte — dá a
/// paginação do PDF de graça (uma bancada = uma página), sem precisar de um segundo laço nem
/// de campo novo carimbado em cada posição: a página de cada peça é <c>Y / comprimentoBancada</c>.
/// </summary>
public static class Bancada
{
    /// <summary>
    /// Onde a peça pousa de verdade, respeitando a linha da bancada. A peça desce até
    /// <paramref name="y"/> por gravidade; se dali ela cruzaria a linha, desce mais um pouco —
    /// até o começo da bancada seguinte. Descer mais nunca cria sobreposição *desde que
    /// aplicado ANTES da posição virar espaço ocupado* (a mesma regra que já valia pra esta
    /// peça continua valendo pras próximas, só que a partir de mais embaixo).
    /// </summary>
    public static int Empurrar(int y, int alturaEmCelulas, int? linhasDaBancada)
    {
        if (linhasDaBancada is not { } linhas || linhas <= 0) return y;
        var dentro = y % linhas;
        return dentro + alturaEmCelulas <= linhas ? y : y - dentro + linhas;
    }

    /// <summary>Mesma ideia de <see cref="Empurrar"/>, em centímetros — pro motor de retângulo, que já trabalha em cm, não em células de grade.</summary>
    public static double Empurrar(double y, double altura, double? comprimentoBancadaCm)
    {
        if (comprimentoBancadaCm is not { } comprimento || comprimento <= 0) return y;
        var dentro = y % comprimento;
        return dentro + altura <= comprimento ? y : y - dentro + comprimento;
    }

    /// <summary>A bancada em CÉLULAS da grade (§11.2), pro motor de contorno. <c>null</c> quer dizer rolo sem fim — como o programa sempre funcionou antes desta funcionalidade.</summary>
    public static int? EmCelulas(double? comprimentoBancadaCm, double passoCm)
    {
        if (comprimentoBancadaCm is not { } comprimento || comprimento <= 0) return null;
        var linhas = (int)Math.Floor(comprimento / passoCm);
        return linhas > 0 ? linhas : null;
    }
}
