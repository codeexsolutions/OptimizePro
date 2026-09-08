using System.Security.Cryptography;
using System.Text;

namespace OptimizePro.Services.Arquivos;

public sealed class ArquivoService : IArquivoService
{
    private static readonly byte[] AssinaturaPng = [0x89, (byte)'P', (byte)'N', (byte)'G'];
    private static readonly byte[] AssinaturaJpg = [0xFF, 0xD8];
    private const string Base36 = "0123456789abcdefghijklmnopqrstuvwxyz";

    public string? DetectarExtensao(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 4 && bytes[..4].SequenceEqual(AssinaturaPng))
            return "png";

        if (bytes.Length >= 2 && bytes[..2].SequenceEqual(AssinaturaJpg))
            return "jpg";

        if (bytes.Length >= 12 &&
            Ascii(bytes[..4]) == "RIFF" &&
            Ascii(bytes[8..12]) == "WEBP")
            return "webp";

        if (bytes.Length >= 3 && Ascii(bytes[..3]) == "GIF")
            return "gif";

        return null;
    }

    private static string Ascii(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes);

    public string GerarNomeSemColisao(string prefixo, string extensao)
    {
        var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var sufixo = RandomBase36(6);
        return $"{prefixo}-{timestampMs}-{sufixo}.{extensao}";
    }

    private static string RandomBase36(int tamanho)
    {
        Span<char> chars = stackalloc char[tamanho];
        for (var i = 0; i < tamanho; i++)
            chars[i] = Base36[RandomNumberGenerator.GetInt32(Base36.Length)];
        return new string(chars);
    }

    public async Task<string> SalvarAsync(string pasta, string nomeArquivo, byte[] bytes, CancellationToken ct = default)
    {
        Directory.CreateDirectory(pasta);
        var caminho = Path.Combine(pasta, nomeArquivo);
        await File.WriteAllBytesAsync(caminho, bytes, ct);
        return caminho;
    }

    public void LimparOrfaos(string pasta, IEnumerable<string> arquivosEmUso, IEnumerable<string> candidatosARemover)
    {
        var emUso = new HashSet<string>(arquivosEmUso, StringComparer.OrdinalIgnoreCase);

        foreach (var candidato in candidatosARemover)
        {
            if (emUso.Contains(candidato)) continue;

            try
            {
                var caminho = Path.Combine(pasta, candidato);
                if (File.Exists(caminho)) File.Delete(caminho);
            }
            catch
            {
                // ignorado — mesma semântica de unlink com erro ignorado do original (§6).
            }
        }
    }
}
