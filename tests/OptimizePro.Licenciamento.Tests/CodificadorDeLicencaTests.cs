using System.Security.Cryptography;
using FluentAssertions;
using OptimizePro.Licenciamento;

namespace OptimizePro.Licenciamento.Tests;

public class CodificadorDeLicencaTests
{
    private static (ECDsa Privada, ECDsa Publica) NovoParDeChaves()
    {
        var chave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publica = ECDsa.Create();
        publica.ImportSubjectPublicKeyInfo(chave.ExportSubjectPublicKeyInfo(), out _);
        return (chave, publica);
    }

    [Fact]
    public void GerarEVerificar_CodigoValido_DevolveAsMesmasInformacoes()
    {
        var (privada, publica) = NovoParDeChaves();
        var validoAte = new DateOnly(2026, 12, 31);
        var clienteIdHash = CodificadorDeLicenca.HashDoCliente("cliente@exemplo.com");

        var codigo = CodificadorDeLicenca.Gerar(privada, validoAte, clienteIdHash, TipoDeLicenca.Paga);
        var info = CodificadorDeLicenca.Verificar(codigo, publica);

        info.Should().NotBeNull();
        info!.ValidoAte.Should().Be(validoAte);
        info.ClienteIdHash.Should().Be(clienteIdHash);
        info.Tipo.Should().Be(TipoDeLicenca.Paga);
    }

    [Fact]
    public void Gerar_CodigoDeTeste_TipoVemComoTeste()
    {
        var (privada, publica) = NovoParDeChaves();
        var codigo = CodificadorDeLicenca.Gerar(privada, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7), 0, TipoDeLicenca.Teste);

        CodificadorDeLicenca.Verificar(codigo, publica)!.Tipo.Should().Be(TipoDeLicenca.Teste);
    }

    [Fact]
    public void Verificar_ComChavePublicaErrada_DevolveNulo()
    {
        var (privada, _) = NovoParDeChaves();
        var (_, publicaDeOutroPar) = NovoParDeChaves(); // par DIFERENTE — simula alguém sem a chave privada certa.

        var codigo = CodificadorDeLicenca.Gerar(privada, new DateOnly(2026, 1, 1), 0, TipoDeLicenca.Paga);

        CodificadorDeLicenca.Verificar(codigo, publicaDeOutroPar).Should().BeNull();
    }

    [Fact]
    public void Verificar_CodigoAdulterado_DevolveNulo()
    {
        // Prova o ponto central do desenho: sem a chave privada, não dá pra forjar/editar um
        // código válido — mudar QUALQUER caractere (aqui, tentar "esticar" a validade) invalida
        // a assinatura inteira.
        var (privada, publica) = NovoParDeChaves();
        var codigo = CodificadorDeLicenca.Gerar(privada, new DateOnly(2026, 1, 1), 0, TipoDeLicenca.Paga);

        var adulterado = (codigo[0] == 'A' ? 'B' : 'A') + codigo[1..];

        CodificadorDeLicenca.Verificar(adulterado, publica).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("nao-eh-um-codigo-valido")]
    [InlineData("00000-00000")]
    public void Verificar_TextoQueNaoEhUmCodigo_DevolveNuloSemLancar(string textoInvalido)
    {
        var (_, publica) = NovoParDeChaves();

        var acao = () => CodificadorDeLicenca.Verificar(textoInvalido, publica);

        acao.Should().NotThrow();
        acao().Should().BeNull();
    }

    [Fact]
    public void Gerar_ComTracosNoFormato_ProduzGruposDeCincoCaracteres()
    {
        var (privada, publica) = NovoParDeChaves();
        var codigo = CodificadorDeLicenca.Gerar(privada, new DateOnly(2026, 6, 15), 123, TipoDeLicenca.Paga);

        codigo.Split('-').Should().AllSatisfy(grupo => grupo.Length.Should().BeLessThanOrEqualTo(5));
        // Espaços/traços são ignorados na decodificação — cola com traço ou sem, dá igual.
        CodificadorDeLicenca.Verificar(codigo.Replace("-", ""), publica).Should().Be(CodificadorDeLicenca.Verificar(codigo, publica));
    }

    [Fact]
    public void HashDoCliente_MesmoIdentificadorComCasingDiferente_DevolveOMesmoHash()
    {
        CodificadorDeLicenca.HashDoCliente("Cliente@Exemplo.com").Should().Be(CodificadorDeLicenca.HashDoCliente(" cliente@exemplo.com "));
    }
}
