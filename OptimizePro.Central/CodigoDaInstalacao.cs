using System.Security.Cryptography;

namespace OptimizePro.Central;

/// <summary>Código curto de 6 caracteres pra login no painel remoto (§24.4) — alfabeto sem caracteres ambíguos (sem 0/O, 1/I/L).</summary>
public static class CodigoDaInstalacao
{
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Gerar()
    {
        Span<char> codigo = stackalloc char[6];
        for (var i = 0; i < codigo.Length; i++)
            codigo[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        return new string(codigo);
    }
}
