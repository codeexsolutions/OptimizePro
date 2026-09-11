using System.Security.Cryptography;

namespace OptimizePro.Central;

/// <summary>
/// Mesmo algoritmo/parâmetros de <c>OptimizePro.Painel.HashDeSenha</c> (PBKDF2-HMACSHA256,
/// 210.000 iterações) — duplicado de propósito (isolamento §23/§24: a Central nunca
/// referencia o Painel), mas precisa ser IDÊNTICO. Antes (§24.4) a Central só verificava
/// senhas geradas pelo app desktop; a partir da §24.7 (gestão de usuário direto no painel
/// remoto) ela também GERA hash — usuário criado ou com senha redefinida pela Central nunca
/// mais passa pelo desktop.
/// </summary>
public static class HashDeSenha
{
    private const int TamanhoDoSalEmBytes = 16;
    private const int TamanhoDoHashEmBytes = 32;
    private const int Iteracoes = 210_000;

    public static (byte[] Hash, byte[] Sal) Gerar(string senha)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanhoDoSalEmBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, HashAlgorithmName.SHA256, TamanhoDoHashEmBytes);
        return (hash, sal);
    }

    public static bool Conferir(string senha, byte[] hashEsperado, byte[] sal)
    {
        var hashCalculado = Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, HashAlgorithmName.SHA256, hashEsperado.Length);
        return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
    }
}
