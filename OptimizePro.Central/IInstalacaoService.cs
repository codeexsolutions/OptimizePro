namespace OptimizePro.Central;

/// <summary>
/// Resultado de provisionar — só traz <see cref="ChaveDeApi"/> quando a instalação acabou de
/// ser criada (§24.1): reprovisionar uma já existente (mesmo <c>ClienteIdHash</c>) devolve a
/// instalação de novo, mas nunca a chave — ela só existe em texto puro no instante da criação.
/// </summary>
public sealed record ResultadoDoProvisionamento(Instalacao Instalacao, string? ChaveDeApi, bool JaExistia);

public interface IInstalacaoService
{
    Task<ResultadoDoProvisionamento> ProvisionarAsync(uint clienteIdHash, string? nomeDaFabrica, CancellationToken ct = default);

    /// <summary>Só confere a identidade (chave de API contra o hash guardado) — não muda nada no banco. Quem processa uma sincronização de verdade (fase 4) chama <see cref="RegistrarSincronizacaoAsync"/> à parte.</summary>
    Task<Instalacao?> AutenticarAsync(string instalacaoId, string chaveDeApi, CancellationToken ct = default);

    Task RegistrarSincronizacaoAsync(string instalacaoId, CancellationToken ct = default);
}
