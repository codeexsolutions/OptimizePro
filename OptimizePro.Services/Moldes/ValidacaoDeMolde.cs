using OptimizePro.Core.Arte;
using OptimizePro.Data.Entidades;

namespace OptimizePro.Services.Moldes;

/// <summary>
/// Porte de <c>arrumarPeca</c>/<c>arrumarAjuste</c> (§4.2, §8.3, §9.1 da especificação) —
/// validação de domínio que antes vivia no servidor Express como validação de payload HTTP.
/// </summary>
internal static class ValidacaoDeMolde
{
    /// <summary>Retorna a peça pronta pra persistir, ou null se inválida (contorno &lt;3 pontos, largura/altura ≤0, ou sem papel).</summary>
    public static MoldePeca? ArrumarPeca(PecaEntrada entrada, int ordem)
    {
        if (string.IsNullOrWhiteSpace(entrada.Papel)) return null;
        if (entrada.Largura <= 0 || entrada.Altura <= 0) return null;
        if (entrada.Contorno.Count < 3) return null;

        return new MoldePeca
        {
            Tamanho = string.IsNullOrWhiteSpace(entrada.Tamanho) ? "único" : entrada.Tamanho.Trim(),
            Papel = entrada.Papel.Trim(),
            Nome = string.IsNullOrWhiteSpace(entrada.Nome) ? null : entrada.Nome.Trim(),
            Quantidade = Math.Max(1, entrada.Quantidade),
            Largura = entrada.Largura,
            Altura = entrada.Altura,
            Contorno = entrada.Contorno,
            Furos = entrada.Furos,
            Origem = entrada.Origem,
            Ordem = ordem,
        };
    }

    /// <summary>modo default "cobrir"; escala clamp [10,400] default 100; giro arredondado ao múltiplo de 90 mais próximo, normalizado [0,360) (§4.2).</summary>
    public static AjusteArte ArrumarAjuste(AjusteArte? entrada)
    {
        if (entrada is null)
            return new AjusteArte(TipoArte.Arte, ModoEncaixeArte.Cobrir, 100, 0, 0, 0, null);

        var escala = Math.Clamp(entrada.EscalaPercentual, 10, 400);
        var giro = ((int)Math.Round(entrada.GirauGraus / 90.0) * 90) % 360;
        if (giro < 0) giro += 360;

        return entrada with { EscalaPercentual = escala, GirauGraus = giro };
    }
}
