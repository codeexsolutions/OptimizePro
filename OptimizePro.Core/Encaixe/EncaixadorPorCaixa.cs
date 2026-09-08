namespace OptimizePro.Core.Encaixe;

public enum HeuristicaDeCaixa { Bl, Bssf, Blsf, Baf }

public readonly record struct EscolhaDeCaixa(double X, double Y, double Largura, double Altura, double P1, double P2);

/// <summary>
/// Porte do encaixador por caixa / MaxRects (§11.4) — mantém a lista de retângulos livres
/// do rolo, escolhe o melhor segundo a heurística (bl/bssf/blsf/baf), recorta o espaço
/// usado e remove sobreposições redundantes. Ignora a silhueta da peça (trata como caixa
/// cheia) ao decidir a posição — quem chama contabiliza a área real separadamente.
/// </summary>
/// <remarks>
/// Separa "encontrar a melhor posição" (não muda estado) de "confirmar" (reserva o
/// espaço), para permitir comparar as pontuações de mais de uma orientação da mesma
/// peça (giro "deitada" — troca largura↔altura) antes de decidir qual usar.
/// </remarks>
public sealed class EncaixadorPorCaixa
{
    private readonly List<RetanguloLivre> _livres;

    public EncaixadorPorCaixa(double larguraTecidoCm, double alturaInicialCm = 1_000_000)
    {
        _livres = [new RetanguloLivre(0, 0, larguraTecidoCm, alturaInicialCm)];
    }

    public EscolhaDeCaixa? EncontrarMelhorPosicao(double largura, double altura, HeuristicaDeCaixa heuristica)
    {
        EscolhaDeCaixa? melhor = null;

        foreach (var livre in _livres)
        {
            if (largura > livre.Largura || altura > livre.Altura)
                continue;

            var (p1, p2) = Pontuar(livre, largura, altura, heuristica);

            if (melhor is not { } atual || p1 < atual.P1 || (p1 == atual.P1 && p2 < atual.P2))
                melhor = new EscolhaDeCaixa(livre.X, livre.Y, largura, altura, p1, p2);
        }

        return melhor;
    }

    /// <summary>Reserva o espaço da escolha: recorta os retângulos livres sobrepostos e remove os redundantes.</summary>
    public void Confirmar(EscolhaDeCaixa escolha)
    {
        var colocado = new RetanguloLivre(escolha.X, escolha.Y, escolha.Largura, escolha.Altura);
        var novos = new List<RetanguloLivre>();

        for (var i = _livres.Count - 1; i >= 0; i--)
        {
            var livre = _livres[i];
            if (!Sobrepoe(livre, colocado))
                continue;

            _livres.RemoveAt(i);

            if (colocado.X > livre.X)
                novos.Add(livre with { Largura = colocado.X - livre.X });
            if (colocado.X + colocado.Largura < livre.X + livre.Largura)
                novos.Add(livre with { X = colocado.X + colocado.Largura, Largura = livre.X + livre.Largura - (colocado.X + colocado.Largura) });
            if (colocado.Y > livre.Y)
                novos.Add(livre with { Altura = colocado.Y - livre.Y });
            if (colocado.Y + colocado.Altura < livre.Y + livre.Altura)
                novos.Add(livre with { Y = colocado.Y + colocado.Altura, Altura = livre.Y + livre.Altura - (colocado.Y + colocado.Altura) });
        }

        _livres.AddRange(novos);
        RemoverRedundantes();
    }

    private static (double P1, double P2) Pontuar(RetanguloLivre livre, double largura, double altura, HeuristicaDeCaixa heuristica) => heuristica switch
    {
        HeuristicaDeCaixa.Bl => (livre.Y, livre.X),
        HeuristicaDeCaixa.Bssf => (Math.Min(livre.Largura - largura, livre.Altura - altura), Math.Max(livre.Largura - largura, livre.Altura - altura)),
        HeuristicaDeCaixa.Blsf => (Math.Max(livre.Largura - largura, livre.Altura - altura), Math.Min(livre.Largura - largura, livre.Altura - altura)),
        HeuristicaDeCaixa.Baf => (livre.Largura * livre.Altura - largura * altura, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(heuristica)),
    };

    private static bool Sobrepoe(RetanguloLivre a, RetanguloLivre b) =>
        a.X < b.X + b.Largura && a.X + a.Largura > b.X && a.Y < b.Y + b.Altura && a.Y + a.Altura > b.Y;

    private void RemoverRedundantes()
    {
        const double epsilon = 1e-9;

        for (var i = _livres.Count - 1; i >= 0; i--)
        {
            for (var j = 0; j < _livres.Count; j++)
            {
                if (i == j) continue;
                if (ContidoEm(_livres[i], _livres[j], epsilon))
                {
                    _livres.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private static bool ContidoEm(RetanguloLivre a, RetanguloLivre b, double epsilon) =>
        a.X >= b.X - epsilon && a.Y >= b.Y - epsilon &&
        a.X + a.Largura <= b.X + b.Largura + epsilon &&
        a.Y + a.Altura <= b.Y + b.Altura + epsilon;
}
