using OptimizePro.Data.Entidades;

namespace OptimizePro.Services.Impressoras;

/// <summary>Fachada da tela de Máquinas (§22.3) — cadastro + varredura de rede.</summary>
public interface IMaquinaService
{
    Task<List<Maquina>> ListarAsync(bool incluirDesabilitadas, CancellationToken ct = default);

    EstadoDaVarredura ObterEstadoDaVarredura();
    event Action<EstadoDaVarredura>? VarreduraAtualizada;

    /// <summary>Retorna false se já existe uma varredura em andamento.</summary>
    bool IniciarVarredura(IReadOnlyList<string> hosts);
    void PararVarredura();

    /// <summary>Cadastra uma máquina pendente (achada na última varredura) com o nome escolhido pelo usuário.</summary>
    Task<Maquina?> CadastrarPendenteAsync(string host, string nome, CancellationToken ct = default);

    bool DescartarPendente(string host);

    /// <summary>Desativa: some das telas e para de ser lida, mas o histórico continua no banco (§ routes/machines.js).</summary>
    Task<bool> DesativarAsync(string id, CancellationToken ct = default);

    Task<bool> ReativarAsync(string id, CancellationToken ct = default);

    /// <summary>Só aceita máquina já desativada — evita perder o histórico com um clique só.</summary>
    Task<(bool Ok, string? Erro)> ExcluirAsync(string id, CancellationToken ct = default);
}
