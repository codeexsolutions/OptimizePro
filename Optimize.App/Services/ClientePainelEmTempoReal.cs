using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using OptimizePro.Servidor;
using OptimizePro.Services.Impressoras;

namespace Optimize.App.Services;

/// <summary>
/// Ponte entre as telas Avalonia e o servidor embutido (§22.6) — a tela local usa o mesmo
/// caminho (SignalR + o endpoint de status) que um painel remoto usaria conectando pelo IP
/// desta máquina, em vez de ganhar um atalho "em processo" que só ela poderia usar.
///
/// Singleton: uma conexão só para o app inteiro, reaproveitada por qualquer tela que precise
/// de atualização ao vivo (hoje só Impressoras) — assim navegar entre telas não acumula
/// conexões SignalR abertas.
/// </summary>
public sealed class ClientePainelEmTempoReal : IAsyncDisposable
{
    private readonly HubConnection _conexao;
    private readonly HttpClient _http;
    private Task? _iniciando;

    public event Action<string>? EventoRecebido;

    public ClientePainelEmTempoReal()
    {
        var baseUrl = $"http://localhost:{ServidorDoPainel.PortaPadrao}";
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _conexao = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hub/painel")
            .WithAutomaticReconnect()
            .Build();

        foreach (var evento in new[] { "new-print", "history-updated", "machine-status" })
            _conexao.On<object>(evento, _ => EventoRecebido?.Invoke(evento));
    }

    /// <summary>Conecta na primeira chamada; chamadas seguintes reaproveitam a mesma conexão (ou a mesma tentativa em andamento).</summary>
    public Task GarantirConectadoAsync()
    {
        if (_conexao.State == HubConnectionState.Connected) return Task.CompletedTask;
        return _iniciando ??= ConectarAsync();
    }

    private async Task ConectarAsync()
    {
        try
        {
            await _conexao.StartAsync();
        }
        catch
        {
            // Servidor embutido pode não ter subido ainda, ou a porta pode estar ocupada — a
            // tela cai pra leitura "pull" (banco local + endpoint de status) sem atualização ao
            // vivo, em vez de travar esperando o hub.
        }
        finally
        {
            _iniciando = null;
        }
    }

    public async Task<IReadOnlyDictionary<string, EstadoDeMaquina>> ObterStatusAsync()
    {
        try
        {
            var status = await _http.GetFromJsonAsync<Dictionary<string, EstadoDeMaquina>>("/api/impressoras/status");
            return status ?? new Dictionary<string, EstadoDeMaquina>();
        }
        catch
        {
            return new Dictionary<string, EstadoDeMaquina>();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _conexao.DisposeAsync();
        _http.Dispose();
    }
}
