using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OptimizePro.Data;
using OptimizePro.Data.Repositorios;
using OptimizePro.Services.Impressoras;
using OptimizePro.Services.Impressoras.Historico;

namespace OptimizePro.Servidor;

/// <summary>
/// Servidor embutido do painel da frota de impressoras (§22.2) — Kestrel + SignalR rodando
/// dentro do processo do <c>Optimize.App</c>, ouvindo em <c>0.0.0.0</c> porque o painel é pra
/// fábrica inteira olhar, não só quem está sentado nesta máquina (mesmo raciocínio do
/// <c>servidor.listen(PORT, "0.0.0.0", ...)</c> do optmize-full).
///
/// Roda ao lado do <see cref="Microsoft.Extensions.Hosting.IHost"/> de DI que já existe pro
/// resto do app desktop (ver <c>App.axaml.cs</c>) — são dois hosts porque o principal não é um
/// host web e não teria como escutar uma porta.
/// </summary>
public sealed class ServidorDoPainel : IAsyncDisposable
{
    private WebApplication? _app;

    /// <summary>Porta padrão do painel — igual à do optmize-full para não confundir quem já conhece o produto.</summary>
    public const int PortaPadrao = 8000;

    public int Porta { get; }

    public ServidorDoPainel(int porta = PortaPadrao) => Porta = porta;

    public async Task IniciarAsync(string caminhoDoBanco)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://0.0.0.0:{Porta}");

        builder.Services.AddSignalR();
        builder.Services.AddDbContext<OptimizeDbContext>(o => o.UseSqlite($"Data Source={caminhoDoBanco}"));

        builder.Services.AddScoped<IMaquinaRepository, MaquinaRepository>();
        builder.Services.AddScoped<IRegistroDeImpressaoRepository, RegistroDeImpressaoRepository>();
        builder.Services.AddScoped<IPedidoRepository, PedidoRepository>();
        builder.Services.AddScoped<IOrdemDeServicoRepository, OrdemDeServicoRepository>();
        builder.Services.AddSingleton<LeitorCsvHistorico>();
        builder.Services.AddSingleton<LeitorXmlHistorico>();
        builder.Services.AddSingleton<LeitorAtBinarioHistorico>();
        builder.Services.AddSingleton<FabricaDeLeitorDeHistorico>();
        builder.Services.AddSingleton<IStatusDeMaquinaStore, StatusDeMaquinaStore>();
        builder.Services.AddHostedService<PollingDeImpressorasService>();

        _app = builder.Build();

        _app.MapHub<PainelHub>("/hub/painel");

        // Estado ao vivo (on-line/off-line) de cada máquina — a leitura "pull" que o painel usa
        // ao abrir/reconectar, antes do primeiro evento "push" do SignalR chegar (§22.6).
        _app.MapGet("/api/impressoras/status", (IStatusDeMaquinaStore store) => Results.Ok(store.ObterTodos()));

        // Endpoint do leitor da calandra (Raspberry Pi, §22.4/§22.8) — porte de GET
        // /api/scan/:code. O código impresso na folha (ver GeradorDeFolhaDePedido) é um hash
        // curto com prefixo: "R" = um trabalho do histórico, "P" = o pedido inteiro. O
        // dispositivo físico ainda está a caminho; o formato já é o mesmo da referência, então
        // não deve precisar mudar quando ele chegar.
        _app.MapGet("/api/scan/{codigo}", async (string codigo, IRegistroDeImpressaoRepository registros, IPedidoRepository pedidos, IOrdemDeServicoRepository ordens) =>
        {
            if (string.IsNullOrEmpty(codigo))
                return Results.BadRequest(new { erro = "codigo_invalido" });

            var prefixo = codigo[0];

            if (prefixo == CodigoDeQr.PrefixoRegistro)
            {
                var todos = await registros.ListarTodosAsync();
                var registro = todos.FirstOrDefault(r => CodigoDeQr.GerarCurto(CodigoDeQr.PrefixoRegistro, r.Id) == codigo);
                if (registro is null) return Results.NotFound(new { erro = "codigo_nao_encontrado" });

                var item = await pedidos.ObterPorRegistroIdAsync(registro.Id);
                object? contextoDoPedido = null;
                if (item is not null)
                {
                    var pedido = await pedidos.ObterAsync(item.PedidoId);
                    var posicao = pedido?.Itens.FindIndex(i => i.Id == item.Id) ?? -1;
                    contextoDoPedido = new
                    {
                        pedidoId = item.PedidoId,
                        pedidoStatus = pedido?.Status,
                        itemId = item.Id,
                        posicao,
                        totalDeItens = pedido?.Itens.Count ?? 0,
                        statusNaCalandra = item.StatusNaCalandra,
                    };
                }

                return Results.Ok(new
                {
                    tipo = "registro",
                    registro.Id,
                    registro.Tarefa,
                    registro.MaquinaId,
                    registro.NomeDaMaquina,
                    registro.Data,
                    registro.Hora,
                    registro.ComprimentoDeImpressao,
                    registro.Status,
                    pedido = contextoDoPedido,
                });
            }

            if (prefixo == CodigoDeQr.PrefixoPedido)
            {
                var todosOsPedidos = await pedidos.ListarAsync();
                var resumo = todosOsPedidos.FirstOrDefault(p => CodigoDeQr.GerarCurto(CodigoDeQr.PrefixoPedido, p.Id) == codigo);
                if (resumo is null) return Results.NotFound(new { erro = "codigo_nao_encontrado" });

                var pedido = await pedidos.ObterAsync(resumo.Id);
                if (pedido is null) return Results.NotFound(new { erro = "codigo_nao_encontrado" });

                return Results.Ok(new
                {
                    tipo = "pedido",
                    pedido.Id,
                    pedido.Status,
                    totalDeItens = pedido.Itens.Count,
                    itens = pedido.Itens.OrderBy(i => i.Posicao).Select(i => new
                    {
                        i.Id,
                        i.Posicao,
                        i.RegistroId,
                        i.Tarefa,
                        i.NomeDaMaquina,
                        i.ComprimentoDeImpressao,
                        i.Data,
                        i.StatusNaCalandra,
                    }),
                });
            }

            if (prefixo == CodigoDeQr.PrefixoOrdemDeServico)
            {
                var todasAsOrdens = await ordens.ListarAsync();
                var resumo = todasAsOrdens.FirstOrDefault(o => CodigoDeQr.GerarCurto(CodigoDeQr.PrefixoOrdemDeServico, o.Id) == codigo);
                if (resumo is null) return Results.NotFound(new { erro = "codigo_nao_encontrado" });

                var ordem = await ordens.ObterAsync(resumo.Id);
                if (ordem is null) return Results.NotFound(new { erro = "codigo_nao_encontrado" });

                return Results.Ok(new
                {
                    tipo = "ordem_de_servico",
                    ordem.Id,
                    ordem.NomeDoCliente,
                    ordem.Tecido,
                    ordem.TamanhoDeImpressao,
                    ordem.Metros,
                    ordem.Maquina,
                    ordem.Data,
                });
            }

            return Results.BadRequest(new { erro = "codigo_invalido" });
        });

        // Marca o resultado de um item de pedido — o que o app da calandra chama depois que o
        // operador confirma na tela dele que o trabalho passou (ou não).
        _app.MapPost("/api/impressoras/pedidos/{pedidoId}/itens/{itemId}/resultado", async (
            string pedidoId, string itemId, ResultadoDoItemRequisicao corpo, IPedidoRepository pedidos, IHubContext<PainelHub> hub) =>
        {
            if (corpo.Status is not ("ok" or "erro"))
                return Results.BadRequest(new { erro = "status deve ser 'ok' ou 'erro'" });

            var item = await pedidos.AtualizarResultadoDoItemAsync(pedidoId, itemId, corpo.Status, corpo.Motivo, corpo.MotivoPersonalizado);
            if (item is null) return Results.NotFound(new { erro = "item_nao_encontrado" });

            await hub.Clients.All.SendAsync("pedido-item-atualizado", new { pedidoId, itemId, status = corpo.Status });
            return Results.Ok(item);
        });

        await _app.StartAsync();
    }

    public async Task PararAsync()
    {
        if (_app is not null)
            await _app.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}

public sealed record ResultadoDoItemRequisicao(string Status, string? Motivo, string? MotivoPersonalizado);
