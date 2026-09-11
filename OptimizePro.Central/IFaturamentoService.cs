namespace OptimizePro.Central;

/// <summary>Mesmo porte de <c>OptimizePro.Painel.ResumoDeFaturamento</c> (§23.2) — duplicado de propósito, mas agora com <see cref="LicencaValidaAte"/> junto ("vence em", §24.8).</summary>
public sealed record ResumoDeFaturamento(
    decimal ValorBaseMensal,
    decimal ValorPorUsuarioExtra,
    int LimiteDeUsuariosNoPlano,
    int UsuariosHabilitados,
    int UsuariosExtras,
    decimal MensalidadeTotal,
    DateOnly? LicencaValidaAte);

public interface IFaturamentoService
{
    /// <summary>
    /// null quando a instalação ainda não sincronizou nenhum lote com dado de faturamento
    /// (app desktop nunca rodou, ou é anterior à ativação da licença) — a tela do painel
    /// remoto trata isso como "ainda sem dados", não como erro.
    /// </summary>
    Task<ResumoDeFaturamento?> ObterAsync(string instalacaoId, CancellationToken ct = default);
}
