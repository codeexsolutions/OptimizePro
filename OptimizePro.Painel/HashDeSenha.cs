using System.Security.Cryptography;

namespace OptimizePro.Painel;

/// <summary>
/// PBKDF2-HMACSHA256 pra senha do painel (§23.1) — sem depender do ASP.NET Identity (que
/// puxaria um monte de infraestrutura de app web que este projeto não usa); só a primitiva de
/// hash, isolada, do jeito que o licenciamento já faz com DPAPI noutro lugar do sistema.
/// </summary>
public static class HashDeSenha
{
    private const int TamanhoDoSalEmBytes = 16;
    private const int TamanhoDoHashEmBytes = 32;
    private const int Iteracoes = 210_000; // recomendação OWASP (2023) pra PBKDF2-HMAC-SHA256

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
