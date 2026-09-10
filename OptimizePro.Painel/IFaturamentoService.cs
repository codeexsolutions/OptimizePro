namespace OptimizePro.Painel;

/// <summary>Porte da regra "até 7 usuários no plano padrão, acima disso cobra por usuário extra" (§23.2, decisão do usuário 10/09/2026).</summary>
public sealed record ResumoDeFaturamento(
    decimal ValorBaseMensal,
    decimal ValorPorUsuarioExtra,
    int LimiteDeUsuariosNoPlano,
    int UsuariosHabilitados,
    int UsuariosExtras,
    decimal MensalidadeTotal);

public interface IFaturamentoService
{
    Task<ConfiguracaoDeFaturamento> ObterConfiguracaoAsync(CancellationToken ct = default);

    Task AtualizarConfiguracaoAsync(decimal valorBaseMensal, decimal valorPorUsuarioExtra, int limiteDeUsuariosNoPlano, CancellationToken ct = default);

    /// <summary>Calcula a mensalidade de agora, a partir da configuração e de quantos usuários estão habilitados neste instante.</summary>
    Task<ResumoDeFaturamento> CalcularMensalidadeAsync(CancellationToken ct = default);
}
