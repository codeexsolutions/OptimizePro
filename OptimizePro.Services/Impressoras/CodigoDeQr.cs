using System.Security.Cryptography;
using System.Text;

namespace OptimizePro.Services.Impressoras;

/// <summary>
/// Porte de <c>services/qrcode.js#qrShortCode</c> — código curto e opaco (prefixo + 10 hex do
/// SHA-1) pra usar como conteúdo do QR, independente do tamanho do id original. A geração do
/// próprio QR (a imagem) usa a biblioteca QRCoder em vez do encoder que a referência escreveu à
/// mão (ver comentário no .csproj).
/// </summary>
public static class CodigoDeQr
{
    public const char PrefixoRegistro = 'R';
    public const char PrefixoPedido = 'P';
    public const char PrefixoOrdemDeServico = 'O';

    public static string GerarCurto(char prefixo, string id)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(id));
        var hex = Convert.ToHexStringLower(hash)[..10];
        return $"{prefixo}{hex}";
    }
}
