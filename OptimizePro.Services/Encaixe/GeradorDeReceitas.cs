using OptimizePro.Core.Encaixe;
using OptimizePro.Core.Encaixe.Busca;

namespace OptimizePro.Services.Encaixe;

/// <summary>
/// Lista padrão de receitas (§11.7/§11.9 — "modo automático: contorno+retângulo
/// disputando"). Os quatro agrupamentos padrão do original (§11.8) já entram na disputa
/// (01/09/2026) — `AGRUPAMENTOS_PADRAO = ["dupla", "solta", "trio", "cruzada"]`: dupla/trio
/// juntam cópias da MESMA peça (a invertida fecha o vão da outra); cruzada junta peças de
/// FORMATOS diferentes (a pequena no vão da grande). Só <see cref="AgrupamentoDeEncaixe.Quarteto"/>
/// fica fora — o próprio original mediu e não compensou o custo de mais uma receita. Motores
/// faixas/NFP documentados na spec como fora da lista padrão, ou não wireados ainda aqui.
/// </summary>
internal static class GeradorDeReceitas
{
    private static readonly CriterioDeOrdem[] OrdensDeContorno = [CriterioDeOrdem.Area, CriterioDeOrdem.Altura, CriterioDeOrdem.Lado];
    // Contato (§9.3) foi testada aqui e REVERTIDA — nunca venceu no lote real e ainda diluía o
    // orçamento de busca das outras receitas (+50% de receitas na família contorno). Continua
    // disponível e testada no Core (HeuristicaDeContorno.Contato), só fora do padrão — ver
    // ESPECIFICACAO-PARA-DOTNET.md §11.3.
    private static readonly HeuristicaDeContorno[] HeuristicasDeContorno = [HeuristicaDeContorno.Fundo, HeuristicaDeContorno.Vazio];
    private static readonly AgrupamentoDeEncaixe[] AgrupamentosDeContorno =
        [AgrupamentoDeEncaixe.Dupla, AgrupamentoDeEncaixe.Solta, AgrupamentoDeEncaixe.Trio, AgrupamentoDeEncaixe.Cruzada];

    private static readonly CriterioDeOrdem[] OrdensDeRetangulo =
        [CriterioDeOrdem.Area, CriterioDeOrdem.Altura, CriterioDeOrdem.Lado, CriterioDeOrdem.Largura];
    private static readonly HeuristicaDeCaixa[] HeuristicasDeRetangulo =
        [HeuristicaDeCaixa.Bl, HeuristicaDeCaixa.Bssf, HeuristicaDeCaixa.Blsf, HeuristicaDeCaixa.Baf];

    /// <summary>"Como encaixar" (§9.3 do guia da tela) — automático deixa os dois motores disputarem; forçar um só faz sentido quando o corte já é sempre pela tesoura (caixa) ou sempre precisa aproveitar a silhueta (contorno).</summary>
    public static IReadOnlyList<Receita> GerarPadrao(ModoDeEncaixe modo = ModoDeEncaixe.Automatico)
    {
        List<Receita> receitas = [];

        if (modo != ModoDeEncaixe.SempreCaixa)
        {
            foreach (var agrupamento in AgrupamentosDeContorno)
                foreach (var ordem in OrdensDeContorno)
                    foreach (var heuristica in HeuristicasDeContorno)
                        receitas.Add(Receita.DeContorno(agrupamento, ordem, heuristica));

            // NFP (§11.5) também respeita a silhueta real — entra no mesmo grupo do contorno,
            // fora quando o corte é sempre pela tesoura (caixa).
            receitas.Add(Receita.DeNfp());
        }

        if (modo != ModoDeEncaixe.SempreContorno)
            foreach (var ordem in OrdensDeRetangulo)
                foreach (var heuristica in HeuristicasDeRetangulo)
                    receitas.Add(Receita.DeRetangulo(ordem, heuristica));

        return receitas;
    }
}
