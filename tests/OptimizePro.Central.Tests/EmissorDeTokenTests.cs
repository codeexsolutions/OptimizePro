using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;

namespace OptimizePro.Central.Tests;

public class EmissorDeTokenTests
{
    private static EmissorDeToken NovoEmissor() =>
        new(new ConfiguracaoDoJwt { ChaveSecreta = "chave-de-teste-com-pelo-menos-32-caracteres" });

    [Fact]
    public void Emitir_TokenTrazOsClaimsEsperados()
    {
        var emissor = NovoEmissor();
        var usuario = new UsuarioAutenticado("inst-1", "u1", "dono", "Dono da Fábrica", true, ["Historico", "Pedidos"]);

        var token = emissor.Emitir(usuario);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == ClaimsDoPainel.InstalacaoId && c.Value == "inst-1");
        jwt.Claims.Should().Contain(c => c.Type == ClaimsDoPainel.EhAdministrador && c.Value == "true");
        jwt.Claims.Where(c => c.Type == ClaimsDoPainel.ModuloLiberado).Select(c => c.Value)
            .Should().BeEquivalentTo(["Historico", "Pedidos"]);
    }

    [Fact]
    public void Emitir_TokenExpiraNoFuturo()
    {
        var emissor = NovoEmissor();
        var token = emissor.Emitir(new UsuarioAutenticado("inst-1", "u1", "dono", "Dono", false, []));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }
}
