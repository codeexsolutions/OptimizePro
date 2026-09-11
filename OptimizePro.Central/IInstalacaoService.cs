namespace OptimizePro.Central;

/// <summary>
/// Resultado de provisionar — só traz <see cref="ChaveDeApi"/> quando ESTA MÁQUINA acabou de
/// ganhar uma chave nova (§24.1): uma instalação já existente (mesmo <c>ClienteIdHash</c>) é
/// reencontrada em vez de duplicada, mas cada <c>MaquinaId</c> ainda ganha sua própria chave na
/// primeira vez que aparece — só uma máquina que já tinha chave e perdeu o arquivo local (ex.:
/// reinstalação) reprovisiona sem receber chave nova, porque ela só existe em texto puro no
/// instante da criação (gap conhecido, sem rota de recuperação ainda).
/// </summary>
public sealed record ResultadoDoProvisionamento(Instalacao Instalacao, string? ChaveDeApi, bool JaExistia);

public interface IInstalacaoService
{
    Task<ResultadoDoProvisionamento> ProvisionarAsync(uint clienteIdHash, string maquinaId, string? nomeDaFabrica, CancellationToken ct = default);

    /// <summary>Só confere a identidade (chave de API contra o hash guardado) — não muda nada no banco. Quem processa uma sincronização de verdade (fase 4) chama <see cref="RegistrarSincronizacaoAsync"/> à parte.</summary>
    Task<Instalacao?> AutenticarAsync(string instalacaoId, string chaveDeApi, CancellationToken ct = default);

    Task RegistrarSincronizacaoAsync(string instalacaoId, CancellationToken ct = default);
}
