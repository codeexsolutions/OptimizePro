namespace OptimizePro.Core.Impressoras;

/// <summary>
/// Família de impressora, identificada pelos arquivos que ela deixa no compartilhamento
/// de rede (porte do optmize-full, domínio "frota de impressoras" — sem protocolo direto
/// com a impressora, só leitura do compartilhamento SMB).
/// </summary>
public enum TipoDeMaquina
{
    /// <summary>Apelido "printer2" na referência — histórico em CSV.</summary>
    Csv,

    /// <summary>Histórico em XML.</summary>
    Xml,

    /// <summary>Formato binário próprio, com duas subvariantes (06/07).</summary>
    AtBinario,
}
