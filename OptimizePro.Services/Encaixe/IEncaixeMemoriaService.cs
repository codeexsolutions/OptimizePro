namespace OptimizePro.Services.Encaixe;

/// <summary>
/// Metade "memória de aprendizado" de <c>IEncaixeService</c> (§6.3/§11.12/§12/§12.1 —
/// <c>encaixe-memoria.js</c> + <c>encaixe-rede.js</c>). <c>EncaixeService.BuscarMelhorEncaixeAsync</c>
/// consome <see cref="ConsultarMemoriaAsync"/> pra pesar a passada base da busca e chama
/// <see cref="RegistrarResultadoDaBuscaAsync"/> ao final, registrando a busca inteira de uma vez.
/// </summary>
public interface IEncaixeMemoriaService
{
    Task<MemoriaDoTipo> ConsultarMemoriaAsync(string assinatura, CancellationToken ct = default);

    /// <summary>Upsert incremental de usos/vitórias de UMA receita; sempre insere uma linha no histórico. Retorna <c>encaixesDoTipo</c> (§4.4).</summary>
    Task<int> RegistrarResultadoAsync(RegistroDeEncaixe registro, CancellationToken ct = default);

    /// <summary>
    /// Registra o placar de uma busca inteira de uma vez (§11.12/§12.1): cada linha de
    /// <see cref="ResultadoDeBuscaParaMemoria.Placar"/> ganha <c>usos+=1</c> incondicional, e
    /// <c>vitorias+=1</c> só a que bate com <see cref="ResultadoDeBuscaParaMemoria.ReceitaVencedora"/>.
    /// Insere uma linha no histórico com <c>features</c>/<c>placar</c> crus (matéria-prima do
    /// treino da rede) e dispara um possível retreino (nunca lança — falha de treino não derruba
    /// o salvamento). Retorna <c>encaixesDoTipo</c>.
    /// </summary>
    Task<int> RegistrarResultadoDaBuscaAsync(ResultadoDeBuscaParaMemoria resultado, CancellationToken ct = default);

    Task<EncaixeGuardadoDto?> BuscarGuardadoAsync(string chave, CancellationToken ct = default);

    /// <summary>Só substitui se o consumo novo for estritamente menor que o guardado (empate não troca, §12). Retorna se guardou.</summary>
    Task<bool> GuardarSeMelhorAsync(EncaixeGuardadoDto guardado, CancellationToken ct = default);

    Task LimparMemoriaAsync(CancellationToken ct = default);
}
