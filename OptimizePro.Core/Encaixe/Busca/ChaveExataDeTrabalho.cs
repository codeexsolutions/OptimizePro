using System.Text;

namespace OptimizePro.Core.Encaixe.Busca;

/// <summary>Uma peça (com quantidade e giro) pro cálculo da chave exata — não precisa ser exatamente <c>PecaParaEncaixar</c> de Services pra não criar dependência de camada.</summary>
public sealed record ItemParaChaveExata(string Id, IReadOnlyList<PontoXY> Contorno, int Quantidade, TipoDeGiro Giro);

/// <summary>
/// Porte da "chave exata" (§11.12) — identifica o trabalho EXATO (peças+quantidades+giro+
/// contorno+largura+folga+margem), diferente da <see cref="AssinaturaDeTrabalho"/> que agrupa
/// trabalhos parecidos. Usada pra reoferecer o melhor encaixe físico já obtido pra ESTE mesmo
/// trabalho (`encaixe_guardados`, §3.6/§12) — hash FNV-1a, igual à especificação original.
/// </summary>
public static class ChaveExataDeTrabalho
{
    public static string Calcular(IReadOnlyList<ItemParaChaveExata> itens, double larguraTecidoCm, double espacoCm, double margemCm)
    {
        var texto = new StringBuilder();
        texto.Append('l').Append(larguraTecidoCm.ToString("0.###")).Append('|');
        texto.Append('e').Append(espacoCm.ToString("0.###")).Append('|');
        texto.Append('m').Append(margemCm.ToString("0.###")).Append('|');

        foreach (var item in itens.OrderBy(i => i.Id, StringComparer.Ordinal))
        {
            texto.Append(item.Id).Append(':').Append(item.Quantidade).Append(':').Append(item.Giro).Append(':');
            foreach (var p in item.Contorno)
                texto.Append(p.X.ToString("0.##")).Append(',').Append(p.Y.ToString("0.##")).Append(';');
            texto.Append('|');
        }

        return Fnv1a(texto.ToString());
    }

    private static string Fnv1a(string texto)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(texto))
        {
            hash ^= b;
            hash *= 16777619u;
        }
        return hash.ToString("x8");
    }
}
