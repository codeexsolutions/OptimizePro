using System.Collections.Concurrent;

namespace OptimizePro.Services.Impressoras;

/// <summary>Porte de <c>services/machineStatus.js</c> — só o "está respondendo agora?" de cada máquina, mantido em memória (não é histórico, é estado ao vivo).</summary>
public sealed record EstadoDeMaquina(bool Online, string? UltimoErro, DateTime AtualizadoEm);

/// <summary>Compartilhado entre o <see cref="PollingDeImpressorasService"/> (que escreve) e o endpoint HTTP de status (que lê) — a mesma leitura "pull" que o painel usa ao abrir/reconectar, antes do primeiro evento "push" do SignalR chegar.</summary>
public interface IStatusDeMaquinaStore
{
    void Definir(string maquinaId, bool online, string? erro = null);
    IReadOnlyDictionary<string, EstadoDeMaquina> ObterTodos();
}

public sealed class StatusDeMaquinaStore : IStatusDeMaquinaStore
{
    private readonly ConcurrentDictionary<string, EstadoDeMaquina> _estados = new();

    public void Definir(string maquinaId, bool online, string? erro = null) =>
        _estados[maquinaId] = new EstadoDeMaquina(online, erro, DateTime.UtcNow);

    public IReadOnlyDictionary<string, EstadoDeMaquina> ObterTodos() => _estados;
}
