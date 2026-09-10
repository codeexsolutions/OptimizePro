using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;
using OptimizePro.Services.Impressoras;
using OptimizePro.Services.Impressoras.Historico;

namespace OptimizePro.Servidor;

/// <summary>
/// Porte (reduzido) de <c>impressoras/services/realtime.js</c> — substitui o
/// <c>setInterval</c>+socket.io da referência por um <see cref="BackgroundService"/> do .NET
/// + SignalR (§22.6). A cada volta: relê a assinatura de cada máquina habilitada (barato) e só
/// releva o histórico de verdade (caro, é rede) quando ela mudou, salvando com a mesma
/// proteção de tinta do <see cref="RegistroDeImpressaoRepository"/>.
///
/// <b>Não portado</b>: o streaming de percentual de progresso das máquinas AT (06/07) enquanto
/// imprimem (<c>emitAtProgress</c> na referência) e os alertas de nível de tinta baixa (XML) —
/// os dois dependem de subsistemas próprios (<c>inkLevelState.js</c>, o parse ao vivo do AT)
/// que ainda não foram portados. O que chega aqui é "máquina on-line/off-line" e "trabalho novo
/// apareceu/terminou", que já é o suficiente pra um painel útil.
/// </summary>
public sealed class PollingDeImpressorasService(
    IServiceScopeFactory scopeFactory, IHubContext<PainelHub> hub, IStatusDeMaquinaStore statusStore)
    : BackgroundService
{
    // 1.5s na referência (io local, sem custo real); aqui a assinatura já filtra o que
    // importa, então um intervalo mais folgado poupa handles de arquivo/rede sem atraso
    // perceptível pra quem olha o painel.
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, EstadoInternoDaMaquina> _estado = [];

    private sealed record EstadoInternoDaMaquina(string Assinatura, HashSet<string> IdsConhecidos, bool Online);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            await TickAsync(stoppingToken);
        } while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var escopo = scopeFactory.CreateScope();
        var maquinaRepo = escopo.ServiceProvider.GetRequiredService<IMaquinaRepository>();
        var registroRepo = escopo.ServiceProvider.GetRequiredService<IRegistroDeImpressaoRepository>();
        var fabrica = escopo.ServiceProvider.GetRequiredService<FabricaDeLeitorDeHistorico>();

        List<Maquina> maquinas;
        try
        {
            maquinas = await maquinaRepo.ListarAsync(incluirDesabilitadas: false, ct);
        }
        catch
        {
            return; // banco temporariamente indisponível — tenta de novo na próxima volta.
        }

        var hoje = DateTime.Now.ToString("yyyy-MM-dd");

        foreach (var maquina in maquinas)
        {
            var leitor = fabrica.ObterPara(maquina.Tipo);
            var anterior = _estado.GetValueOrDefault(maquina.Id);

            try
            {
                var assinatura = await leitor.AssinaturaAsync(maquina, ct);

                if (anterior is not null && anterior.Assinatura == assinatura)
                {
                    if (!anterior.Online) await MarcarOnlineAsync(maquina, ct);
                    continue;
                }

                var registros = await leitor.LerIntervaloAsync(maquina, hoje, hoje, ct);
                await registroRepo.SalvarLoteAsync(registros, ct);

                var idsAtuais = registros.Select(r => r.Id).ToHashSet();
                statusStore.Definir(maquina.Id, true);

                if (anterior is null)
                {
                    // Primeira leitura desta sessão: cria a base sem notificar trabalhos antigos
                    // como se fossem novos — só a partir daqui um id "some do conhecido" conta.
                    _estado[maquina.Id] = new EstadoInternoDaMaquina(assinatura, idsAtuais, true);
                    await hub.Clients.All.SendAsync("machine-status", new { machineId = maquina.Id, machineName = maquina.Nome, online = true }, ct);
                    continue;
                }

                var novos = registros.Where(r => !anterior.IdsConhecidos.Contains(r.Id)).ToList();
                _estado[maquina.Id] = new EstadoInternoDaMaquina(assinatura, idsAtuais, true);

                await hub.Clients.All.SendAsync("history-updated", new
                {
                    machineId = maquina.Id,
                    machineName = maquina.Nome,
                    count = novos.Count,
                    at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                }, ct);

                foreach (var trabalho in novos.Where(r => !r.Cancelada && !r.ComErro).OrderBy(r => r.DataHora))
                    await hub.Clients.All.SendAsync("new-print", trabalho, ct);
            }
            catch (Exception ex)
            {
                statusStore.Definir(maquina.Id, false, ex.Message);
                if (anterior is null || anterior.Online)
                    await hub.Clients.All.SendAsync("machine-status", new { machineId = maquina.Id, machineName = maquina.Nome, online = false, error = ex.Message }, ct);

                _estado[maquina.Id] = anterior is null
                    ? new EstadoInternoDaMaquina("", [], false)
                    : anterior with { Online = false };
            }
        }
    }

    private async Task MarcarOnlineAsync(Maquina maquina, CancellationToken ct)
    {
        statusStore.Definir(maquina.Id, true);
        _estado[maquina.Id] = _estado[maquina.Id] with { Online = true };
        await hub.Clients.All.SendAsync("machine-status", new { machineId = maquina.Id, machineName = maquina.Nome, online = true }, ct);
    }
}
