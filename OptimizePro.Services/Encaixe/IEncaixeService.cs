namespace OptimizePro.Services.Encaixe;

/// <summary>
/// Orquestra o motor puro (<c>OptimizePro.Core.Encaixe</c>) — dispara a busca por receitas
/// sobre os motores de contorno, retângulo e NFP, com agrupamento em blocos (dupla/trio/cruzada)
/// (§6.3/§11.5/§11.7/§11.8/§11.9, "modo automático"). Faixas ainda não é despachado — ver
/// <see cref="GeradorDeReceitas"/>.
/// </summary>
public interface IEncaixeService
{
    /// <exception cref="ArgumentException"><paramref name="pecas"/> vazio, ou nenhuma peça cabe na largura do tecido.</exception>
    Task<ResultadoDeEncaixe> BuscarMelhorEncaixeAsync(
        IReadOnlyList<PecaParaEncaixar> pecas,
        ConfiguracaoDeEncaixe config,
        IProgress<AndamentoDoEncaixe>? progresso = null,
        CancellationToken cancelamento = default);
}
