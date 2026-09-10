using OptimizePro.Data.Entidades;

namespace OptimizePro.Services.Impressoras.Historico;

/// <summary>
/// Um leitor de histórico por família de impressora (csv/xml/at-binário, §22.4) — porte de
/// <c>impressoras/sources/*.js</c>. Só lê o compartilhamento de rede; quem grava no banco é o
/// <see cref="SincronizadorDeHistoricoService"/>, que também usa <see cref="AssinaturaAsync"/>
/// pra pular a releitura cara quando o arquivo não mudou desde a última sincronização.
/// </summary>
public interface ILeitorDeHistorico
{
    Task<List<RegistroDeImpressao>> LerIntervaloAsync(Maquina maquina, string dataInicioIso, string dataFimIso, CancellationToken ct = default);

    /// <summary>Tamanho+data de modificação do(s) arquivo(s) de origem — barato de calcular, caro de comparar registro a registro.</summary>
    Task<string> AssinaturaAsync(Maquina maquina, CancellationToken ct = default);
}
