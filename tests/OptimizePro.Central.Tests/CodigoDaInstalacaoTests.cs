using FluentAssertions;

namespace OptimizePro.Central.Tests;

public class CodigoDaInstalacaoTests
{
    [Fact]
    public void Gerar_ProduzSeisCaracteres()
    {
        CodigoDaInstalacao.Gerar().Should().HaveLength(6);
    }

    [Fact]
    public void Gerar_NaoUsaCaracteresAmbiguos()
    {
        var codigo = CodigoDaInstalacao.Gerar();
        codigo.Should().NotContainAny("0", "O", "1", "I", "L");
    }

    [Fact]
    public void Gerar_ChamadasDiferentesProduzemCodigosDiferentes()
    {
        var codigos = Enumerable.Range(0, 20).Select(_ => CodigoDaInstalacao.Gerar()).Distinct();
        codigos.Should().HaveCount(20, "com 32^6 combinações, 20 gerações não deviam colidir");
    }
}
