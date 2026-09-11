namespace OptimizePro.Central;

/// <summary>
/// Uma máquina sincronizando dentro de uma instalação (§24.1) — várias máquinas da mesma
/// gráfica compartilham UMA licença/instalação (mesmo <see cref="Instalacao.ClienteIdHash"/>),
/// mas cada uma precisa da própria chave de API: sem isso, só a primeira máquina a provisionar
/// conseguiria sincronizar (a segunda receberia "já existe" sem chave nenhuma pra usar — gap
/// que existiu até 11/09/2026).
/// </summary>
public class ChaveDeMaquina
{
    public required string InstalacaoId { get; set; }

    /// <summary>Gerado uma vez por máquina física e persistido localmente (ver <c>OptimizePro.Sincronizacao.IdentidadeDaMaquina</c>) — não é segredo, só precisa ser estável entre reinicializações/reinstalações do app.</summary>
    public required string MaquinaId { get; set; }

    /// <summary>Hash SHA-256 da chave de API — a chave em si só existe no instante da criação, nunca fica recuperável depois (mesmo padrão de <see cref="Administrador.SenhaHash"/>, mas sem sal: a chave já nasce de alta entropia).</summary>
    public required byte[] ChaveHash { get; set; }

    public DateTime CriadoEm { get; set; }

    public Instalacao? Instalacao { get; set; }
}
