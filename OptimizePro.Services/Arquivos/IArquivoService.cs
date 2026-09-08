namespace OptimizePro.Services.Arquivos;

/// <summary>
/// Substitui <c>uploads-arquivos.js</c> (§6 da especificação): detecção de tipo por
/// assinatura binária, nome sem colisão, e a faxina de arquivos órfãos.
/// </summary>
public interface IArquivoService
{
    /// <summary>Detecta a extensão pela assinatura binária dos bytes (nunca pelo nome/Content-Type declarado). Retorna null se não reconhecido.</summary>
    string? DetectarExtensao(ReadOnlySpan<byte> bytes);

    /// <summary><c>{prefixo}-{timestampMs}-{6 chars base36 aleatórios}.{extensao}</c> (§6).</summary>
    string GerarNomeSemColisao(string prefixo, string extensao);

    Task<string> SalvarAsync(string pasta, string nomeArquivo, byte[] bytes, CancellationToken ct = default);

    /// <summary>
    /// Apaga, dentro de <paramref name="pasta"/>, todo arquivo de <paramref name="candidatosARemover"/>
    /// que não esteja em <paramref name="arquivosEmUso"/> — conjunto que deve vir da tabela
    /// inteira (todos os registros vivos), nunca só do que acabou de mudar (regra crítica, §6).
    /// Erros de exclusão (arquivo já removido, etc.) são ignorados.
    /// </summary>
    void LimparOrfaos(string pasta, IEnumerable<string> arquivosEmUso, IEnumerable<string> candidatosARemover);
}
