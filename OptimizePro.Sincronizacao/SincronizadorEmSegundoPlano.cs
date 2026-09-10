using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OptimizePro.Sincronizacao;

/// <summary>
/// Roda <see cref="SincronizacaoService"/> de tempos em tempos (§24.2) — mesmo padrão do
/// <c>PollingDeImpressorasService</c> (cria seu próprio escopo de DI a cada rodada, porque
/// <c>OptimizeDbContext</c>/<c>PainelDbContext</c> são scoped e isto roda solto, fora de
/// qualquer tela). Falha silenciosa em qualquer erro — sincronização nunca pode derrubar o
/// app desktop nem gerar ruído pro usuário; é sempre best-effort.
/// </summary>
public sealed class SincronizadorEmSegundoPlano(IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            await TentarSincronizarAsync(stoppingToken);
        } while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TentarSincronizarAsync(CancellationToken ct)
    {
        try
        {
            using var escopo = scopeFactory.CreateScope();
            var servico = escopo.ServiceProvider.GetRequiredService<SincronizacaoService>();
            await servico.SincronizarAsync(ct);
        }
        catch
        {
            // Best-effort de verdade: qualquer falha (rede, Central fora do ar, banco local
            // ocupado) só adia pro próximo ciclo, nunca propaga.
        }
    }
}
