namespace OptimizePro.Core.Encaixe.Busca;

public enum MotorDeEncaixe { Contorno, Retangulo, Faixas, Nfp }

/// <summary>Padrão: [Dupla, Solta, Trio, Cruzada] (§11.7/§11.8) — <see cref="Quarteto"/> não entra na disputa (o original mediu e não compensou).</summary>
public enum AgrupamentoDeEncaixe { Solta, Dupla, Trio, Quarteto, Cruzada }

public enum CriterioDeOrdem { Area, Altura, Lado, Largura }

/// <summary>
/// Porte de <c>Receita</c> (§11.7) — uma combinação de motor/agrupamento/ordem/heurística
/// a testar. A heurística é específica do motor (contorno usa <see cref="HeuristicaDeContorno"/>,
/// retângulo usa <see cref="HeuristicaDeCaixa"/>; faixas reaproveita a de contorno por
/// baixo dos panos; NFP não tem escolha — "encosta" fixo, §11.5).
/// </summary>
public sealed record Receita(
    MotorDeEncaixe Motor,
    AgrupamentoDeEncaixe Agrupamento,
    CriterioDeOrdem Ordem,
    HeuristicaDeContorno? HeuristicaContorno = null,
    HeuristicaDeCaixa? HeuristicaCaixa = null)
{
    public static Receita DeContorno(AgrupamentoDeEncaixe agrupamento, CriterioDeOrdem ordem, HeuristicaDeContorno heuristica) =>
        new(MotorDeEncaixe.Contorno, agrupamento, ordem, HeuristicaContorno: heuristica);

    public static Receita DeRetangulo(CriterioDeOrdem ordem, HeuristicaDeCaixa heuristica) =>
        new(MotorDeEncaixe.Retangulo, AgrupamentoDeEncaixe.Solta, ordem, HeuristicaCaixa: heuristica);

    public static Receita DeFaixas(AgrupamentoDeEncaixe agrupamento, CriterioDeOrdem ordem, HeuristicaDeContorno heuristica) =>
        new(MotorDeEncaixe.Faixas, agrupamento, ordem, HeuristicaContorno: heuristica);

    /// <summary>NFP não tem escolha de agrupamento/ordem/heurística real (§11.5) — uma receita canônica só, a busca ainda varia a ORDEM de colocação dos itens entre tentativas.</summary>
    public static Receita DeNfp() => new(MotorDeEncaixe.Nfp, AgrupamentoDeEncaixe.Solta, CriterioDeOrdem.Area);
}
