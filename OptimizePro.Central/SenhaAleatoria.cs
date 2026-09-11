using System.Security.Cryptography;

namespace OptimizePro.Central;

/// <summary>
/// Senha temporária que a staff gera pra um usuário que esqueceu a dele (§26 — aba
/// Administradores do painel). Alfabeto sem caracteres que se confundem no olho (0/O, 1/l/I) —
/// quem recebe essa senha por telefone/WhatsApp precisa digitar certo de primeira.
/// </summary>
public static class SenhaAleatoria
{
    private const string Alfabeto = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Gerar(int tamanho = 10) =>
        string.Create(tamanho, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        });
}
