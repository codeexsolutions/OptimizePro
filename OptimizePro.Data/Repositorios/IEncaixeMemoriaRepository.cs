using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

/// <summary>Memória de aprendizado (§11.12/§11.7 do Core.Encaixe.Busca — placar de receitas, melhor encaixe salvo, histórico).</summary>
public interface IEncaixeMemoriaRepository
{
    Task<EncaixeReceita?> ObterReceitaAsync(string assinatura, string receita, CancellationToken ct = default);

    /// <summary>Upsert por (assinatura, receita): incrementa usos e, se venceu, vitórias.</summary>
    Task RegistrarUsoDeReceitaAsync(string assinatura, string receita, bool venceu, CancellationToken ct = default);

    Task<EncaixeGuardado?> ObterGuardadoAsync(string chave, CancellationToken ct = default);

    /// <summary>Upsert por chave — quem chama decide se vale a pena salvar (consumo menor que o guardado).</summary>
    Task SalvarGuardadoAsync(EncaixeGuardado guardado, CancellationToken ct = default);

    Task RegistrarHistoricoAsync(EncaixeHistorico historico, CancellationToken ct = default);

    /// <summary>Limpa receitas, guardados e histórico — equivalente a <c>LimparMemoriaAsync</c> (§6.3 da arquitetura).</summary>
    Task LimparAsync(CancellationToken ct = default);

    /// <summary>"Camada geral" (§11.12/§12): usos/vitórias somados por receita, através de TODAS as assinaturas.</summary>
    Task<Dictionary<string, (int Usos, int Vitorias)>> SomarUsosEVitoriasPorReceitaAsync(CancellationToken ct = default);

    /// <summary>"Camada do tipo" (§11.12/§12): as linhas de <c>encaixe_receitas</c> só desta assinatura.</summary>
    Task<List<EncaixeReceita>> ListarReceitasPorAssinaturaAsync(string assinatura, CancellationToken ct = default);

    Task<int> ContarHistoricoTotalAsync(CancellationToken ct = default);
    Task<int> ContarHistoricoPorAssinaturaAsync(string assinatura, CancellationToken ct = default);

    /// <summary>Menor <c>Consumo</c> já registrado no histórico desta assinatura — o "melhorAntes"/recorde do tipo (§11.12).</summary>
    Task<double?> MenorConsumoPorAssinaturaAsync(string assinatura, CancellationToken ct = default);

    /// <summary>Pesos atuais da rede das receitas (§3.10.1/§12.1) — nulo se nunca treinada.</summary>
    Task<EncaixeRedePesos?> ObterRedePesosAsync(CancellationToken ct = default);

    /// <summary>Upsert da linha única (id=1) — reescrita a cada retreino.</summary>
    Task SalvarRedePesosAsync(EncaixeRedePesos pesos, CancellationToken ct = default);

    /// <summary>Linhas de <c>encaixe_historico</c> com <c>features</c>/<c>placar</c> preenchidos — matéria-prima de <c>montarExemplosDeTreino</c> (§12.1).</summary>
    Task<List<EncaixeHistorico>> ListarHistoricoParaTreinoAsync(CancellationToken ct = default);

    /// <summary>Assinaturas distintas entre as linhas elegíveis pra treino — mede "diversidade de formatos vistos" (§12.1, <c>REDE_LIMIAR_DIVERSIDADE</c>).</summary>
    Task<int> ContarAssinaturasDistintasParaTreinoAsync(CancellationToken ct = default);
}
