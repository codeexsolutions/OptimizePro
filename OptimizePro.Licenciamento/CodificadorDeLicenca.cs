using System.Security.Cryptography;
using System.Text;

namespace OptimizePro.Licenciamento;

/// <summary>
/// Gera (com a chave PRIVADA, só na ferramenta interna) e verifica (com a chave PÚBLICA,
/// embutida no app do cliente) o código de licença — assinatura digital ECDSA P-256, formato
/// IEEE P1363 (r||s de 32 bytes cada, tamanho fixo, mais compacto que o DER padrão).
/// </summary>
/// <remarks>
/// Formato do blob antes de codificar em Base32: <c>[versao(1) tipo(1) dias(2) clienteIdHash(4)] + assinatura(64)</c>
/// = 72 bytes → ~116 caracteres em Base32 Crockford, com traços a cada 5. Sem a chave privada
/// (que nunca sai da ferramenta de geração) não dá pra forjar um código válido — só decifrar um
/// já existente, o que não ajuda a criar um novo.
/// </remarks>
public static class CodificadorDeLicenca
{
    private const byte VersaoAtual = 1;
    private static readonly DateOnly Epoca = new(2025, 1, 1);
    private const int TamanhoDoPayload = 8; // versao(1) + tipo(1) + dias(2) + clienteIdHash(4)
    private const int TamanhoDaAssinatura = 64; // ECDSA P-256, IEEE P1363 (32+32 bytes)

    /// <summary>Gera o código — precisa da CHAVE PRIVADA (nunca deve rodar dentro do app que vai pro cliente).</summary>
    public static string Gerar(ECDsa chavePrivada, DateOnly validoAte, uint clienteIdHash, TipoDeLicenca tipo)
    {
        var payload = MontarPayload(validoAte, clienteIdHash, tipo);
        var assinatura = chavePrivada.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        var blob = new byte[TamanhoDoPayload + TamanhoDaAssinatura];
        payload.CopyTo(blob, 0);
        assinatura.CopyTo(blob, TamanhoDoPayload);

        return Base32Crockford.ComTracos(Base32Crockford.Codificar(blob));
    }

    /// <summary>
    /// Confere a assinatura com a CHAVE PÚBLICA — devolve <c>null</c> pra qualquer código
    /// inválido (formato errado, traço de digitação, assinatura que não bate), nunca lança:
    /// código digitado errado pelo cliente é entrada normal, não bug.
    /// </summary>
    public static InformacoesDaLicenca? Verificar(string codigo, ECDsa chavePublica)
    {
        var blob = Base32Crockford.Decodificar(codigo);
        if (blob is null || blob.Length != TamanhoDoPayload + TamanhoDaAssinatura)
            return null;

        var payload = blob.AsSpan(0, TamanhoDoPayload).ToArray();
        var assinatura = blob.AsSpan(TamanhoDoPayload, TamanhoDaAssinatura).ToArray();

        bool assinaturaValida;
        try
        {
            assinaturaValida = chavePublica.VerifyData(payload, assinatura, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException)
        {
            return null; // assinatura malformada (ex.: tamanho errado por bit flip na decodificação) — inválida, não erro.
        }

        if (!assinaturaValida) return null;
        if (payload[0] != VersaoAtual) return null;

        var tipo = (TipoDeLicenca)payload[1];
        var dias = (payload[2] << 8) | payload[3];
        var clienteIdHash = (uint)((payload[4] << 24) | (payload[5] << 16) | (payload[6] << 8) | payload[7]);

        return new InformacoesDaLicenca(Epoca.AddDays(dias), clienteIdHash, tipo);
    }

    private static byte[] MontarPayload(DateOnly validoAte, uint clienteIdHash, TipoDeLicenca tipo)
    {
        var dias = validoAte.DayNumber - Epoca.DayNumber;
        if (dias is < 0 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(validoAte), $"Data fora do intervalo suportado (entre {Epoca:yyyy-MM-dd} e {Epoca.AddDays(ushort.MaxValue):yyyy-MM-dd}).");

        return
        [
            VersaoAtual,
            (byte)tipo,
            (byte)(dias >> 8), (byte)dias,
            (byte)(clienteIdHash >> 24), (byte)(clienteIdHash >> 16), (byte)(clienteIdHash >> 8), (byte)clienteIdHash,
        ];
    }

    /// <summary>
    /// Hash curto e estável de um identificador de cliente (nome, e-mail, CNPJ) — só pra
    /// rastrear/auditar de quem é o código se vazar (aparece decodificado se alguém verificar
    /// com a chave pública). NÃO amarra o código a uma máquina/hardware — a licença pedida é
    /// 100% offline e portável, então essa checagem não existe de propósito.
    /// </summary>
    public static uint HashDoCliente(string identificador)
    {
        var bytes = Encoding.UTF8.GetBytes(identificador.Trim().ToLowerInvariant());
        var hash = SHA256.HashData(bytes);
        return (uint)((hash[0] << 24) | (hash[1] << 16) | (hash[2] << 8) | hash[3]);
    }
}
