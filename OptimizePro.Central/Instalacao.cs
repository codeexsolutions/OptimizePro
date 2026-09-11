namespace OptimizePro.Central;

/// <summary>
/// Um tenant da Central (§24) — uma instalação do Optimize (uma fábrica) que decidiu
/// sincronizar dados pra acesso remoto do painel. Identificada por
/// <see cref="ClienteIdHash"/>, o mesmo hash de 32 bits já embutido na licença
/// (<c>OptimizePro.Licenciamento.CodificadorDeLicenca</c>) — não precisa inventar outro id
/// nem pedir pro cliente digitar nada.
/// </summary>
public class Instalacao
{
    public required string Id { get; set; }

    /// <summary>
    /// Código curto (6 chars, sem caracteres ambíguos) pra quem vai logar no painel remoto
    /// (§24.4) digitar — o Id é um GUID, ninguém decora isso; o proprietário/equipe decora
    /// "empresa: AB3X9K". Único, gerado no provisionamento.
    /// </summary>
    public required string Codigo { get; set; }

    /// <summary>Guardado como <see cref="long"/> (cabe um uint inteiro) porque PostgreSQL não tem inteiro sem sinal.</summary>
    public required long ClienteIdHash { get; set; }

    public string? NomeDaFabrica { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? UltimaSincronizacaoEm { get; set; }
}
