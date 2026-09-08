namespace OptimizePro.Services.Vetor;

/// <summary>Orquestra o pipeline puro (<c>OptimizePro.Core.Vetor</c>) — decodifica a imagem, roda os 9 passos de §14.1 e monta o SVG final (§6.4/§9.4 da arquitetura).</summary>
public interface IVetorService
{
    Task<ResultadoDeVetorizacao> VetorizarAsync(byte[] imagemBytes, OpcoesDeVetorizacao opcoes, CancellationToken ct = default);

    /// <summary>
    /// Estima um bom valor inicial de "Cores" pra ESTA imagem (§14.2/achado 02/09/2026: com
    /// poucas cores, o quantizador é obrigado a misturar regiões visualmente diferentes —
    /// ex.: um tom de degradê com o branco de um texto — o que sai como "vazamento" de cor
    /// entre formas sem relação nenhuma). Não substitui o campo "Cores" (que continua
    /// totalmente editável) — só dá um chute inicial melhor que um número fixo de preset.
    /// </summary>
    Task<int> SugerirNumeroDeCoresAsync(byte[] imagemBytes, CancellationToken ct = default);
}
