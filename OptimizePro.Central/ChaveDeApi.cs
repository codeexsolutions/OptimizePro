using System.Security.Cryptography;
using System.Text;

namespace OptimizePro.Central;

/// <summary>
/// Chave de API que cada instalação usa pra sincronizar com a Central (§24.1) — só um segredo
/// aleatório de alta entropia (256 bits), então hash simples (SHA-256) basta; PBKDF2 é pra
/// senha que uma pessoa escolhe (baixa entropia), não pra isto.
/// </summary>
public static class ChaveDeApi
{
    public static string Gerar()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static byte[] Hash(string chave) => SHA256.HashData(Encoding.UTF8.GetBytes(chave));

    public static bool Conferir(string chave, byte[] hashEsperado) =>
        CryptographicOperations.FixedTimeEquals(Hash(chave), hashEsperado);
}
