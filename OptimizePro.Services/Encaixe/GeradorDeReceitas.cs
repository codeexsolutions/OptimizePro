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

        // Vãos (§21.3) — testado, MEDIDO no pool comum e REVERTIDO (mesma política de sempre:
        // sem ganho medido, não vira padrão). Lote real de 25-08 (179cm/60s, 2 rodadas):
        // 251,8cm e 252,0cm, ambos dentro da faixa já documentada sem vãos (251,6-252,8cm) —
        // Contorno venceu as duas vezes, Vãos nunca. A tentativa-count caiu de ~590k pra
        // ~400-440k (receita de vãos é mais cara por descer pelos intervalos, e sem fatia
        // dedicada ela dilui o orçamento das outras receitas na disputa comum) sem compensar em
        // consumo. Bate com o que a própria referência mediu: só ganhou de verdade DEPOIS de dar
        // a ele uma fatia PRÓPRIA (1/8 do orçamento, não misturada no pool). O motor
        // (`EncaixadorPorVaos`, Core) e o despacho (`EncaixeService.ExecutarVaos`) continuam
        // prontos e testados — só a entrada em `GerarPadrao` foi revertida. Próximo passo, se
        // valer a pena revisitar: estender `ParticionamentoDeFatias` (que já reserva fatia
        // dedicada pro NFP, hoje desligado — `reservarUltimaFatiaParaNfp: false` em
        // `BuscarMelhorEncaixeAsync`) pra reservar uma fatia própria também pro motor de vãos, e
        // remedir NESSA configuração antes de decidir de novo.

        if (modo != ModoDeEncaixe.SempreContorno)
            foreach (var ordem in OrdensDeRetangulo)
                foreach (var heuristica in HeuristicasDeRetangulo)
                    receitas.Add(Receita.DeRetangulo(ordem, heuristica));

        return receitas;
    }
}
