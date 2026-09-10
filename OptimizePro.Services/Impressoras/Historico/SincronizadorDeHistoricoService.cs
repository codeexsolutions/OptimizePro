using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Services.Impressoras.Historico;

/// <summary>
/// Junta leitor + repositório + a máquina cadastrada — porte da parte de sincronização de
/// <c>impressoras/services/sync.js</c> (§22.4). A checagem de assinatura evita reler o
/// histórico inteiro (caro, é rede) quando nada mudou desde a última vez.
/// </summary>
public sealed class SincronizadorDeHistoricoService(
    FabricaDeLeitorDeHistorico fabrica, IRegistroDeImpressaoRepository registroRepositorio, IMaquinaRepository maquinaRepositorio)
{
    /// <summary>Retorna a quantidade importada, ou -1 se a assinatura não mudou (pulou a releitura).</summary>
    public async Task<int> SincronizarAsync(Maquina maquina, string dataInicioIso, string dataFimIso, bool forcar = false, CancellationToken ct = default)
    {
        var leitor = fabrica.ObterPara(maquina.Tipo);

        var assinatura = await leitor.AssinaturaAsync(maquina, ct);
        if (!forcar && assinatura.Length > 0 && assinatura == maquina.UltimaAssinaturaHistorico)
            return -1;

        var registros = await leitor.LerIntervaloAsync(maquina, dataInicioIso, dataFimIso, ct);
        var gravados = await registroRepositorio.SalvarLoteAsync(registros, ct);

        maquina.UltimaAssinaturaHistorico = assinatura;
        await maquinaRepositorio.SalvarAsync(maquina, ct);

        return gravados;
    }

    /// <summary>Roda a sincronização em todas as máquinas habilitadas — usado no backfill de uma máquina recém-cadastrada e no polling periódico (§22.5).</summary>
    public async Task<int> SincronizarTodasAsync(string dataInicioIso, string dataFimIso, CancellationToken ct = default)
    {
        var maquinas = await maquinaRepositorio.ListarAsync(incluirDesabilitadas: false, ct);
        var total = 0;
        foreach (var maquina in maquinas)
        {
            var resultado = await SincronizarAsync(maquina, dataInicioIso, dataFimIso, ct: ct);
            if (resultado > 0) total += resultado;
        }
        return total;
    }
}
