using System.Security.Cryptography;

namespace OptimizePro.Central;

/// <summary>
/// Mesmo algoritmo/parâmetros de <c>OptimizePro.Painel.HashDeSenha</c> (PBKDF2-HMACSHA256,
/// 210.000 iterações) — duplicado de propósito (isolamento §23/§24: a Central nunca
/// referencia o Painel), mas precisa ser IDÊNTICO, porque verifica o mesmo hash que o app
/// desktop gerou e sincronizou. Mudar um lado sem mudar o outro quebra o login remoto.
/// </summary>
public static class VerificacaoDeSenha
{
    private const int Iteracoes = 210_000;

    public static bool Conferir(string senha, byte[] hashEsperado, byte[] sal)
    {
        var hashCalculado = Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, HashAlgorithmName.SHA256, hashEsperado.Length);
        return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
    }
}
