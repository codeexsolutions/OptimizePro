using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OptimizePro.Central;

public sealed class ConfiguracaoDoJwt
{
    public required string ChaveSecreta { get; init; }
    public string Emissor { get; init; } = "OptimizePro.Central";
    public TimeSpan Validade { get; init; } = TimeSpan.FromHours(12);
}

/// <summary>Nomes dos claims custom que o React lê do token pra montar o menu (§24.4) — módulos liberados e "é administrador" (vê Usuários/Faturamento).</summary>
public static class ClaimsDoPainel
{
    public const string InstalacaoId = "instalacao_id";
    public const string EhAdministrador = "eh_administrador";
    public const string ModuloLiberado = "modulo_liberado"; // um claim repetido por módulo
}

public sealed class EmissorDeToken(ConfiguracaoDoJwt configuracao)
{
    public string Emitir(UsuarioAutenticado usuario)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.UsuarioId),
            new(JwtRegisteredClaimNames.Name, usuario.Nome),
            new(JwtRegisteredClaimNames.PreferredUsername, usuario.Login),
            new(ClaimsDoPainel.InstalacaoId, usuario.InstalacaoId),
            new(ClaimsDoPainel.EhAdministrador, usuario.EhAdministrador ? "true" : "false"),
        };
        claims.AddRange(usuario.ModulosLiberados.Select(m => new Claim(ClaimsDoPainel.ModuloLiberado, m)));

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuracao.ChaveSecreta));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuracao.Emissor,
            audience: configuracao.Emissor,
            claims: claims,
            expires: DateTime.UtcNow.Add(configuracao.Validade),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
