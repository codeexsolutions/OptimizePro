using FluentAssertions;
using OptimizePro.Services.Encaixe;

namespace OptimizePro.Services.Tests;

public class LeitorDeQuantidadeDoNomeTests
{
    // A função recebe o nome já SEM extensão — quem chama (leitura de imagem/molde) tira a
    // extensão antes, igual ao original (`file.name.replace(/\.[^.]+$/, "")`).
    [Theory]
    [InlineData("frente 5x", "frente", 5)]
    [InlineData("x3 manga", "manga", 3)]
    [InlineData("costas-12x", "costas", 12)]
    [InlineData("manga4x", "manga", 4)]
    [InlineData("manga 2 x", "manga", 2)]
    [InlineData("manga 3 x", "manga", 3)]
    public void Ler_NomeComQuantidade_ExtraiQuantidadeELimpaONome(string arquivo, string nomeEsperado, int qtdEsperada)
    {
        var lido = LeitorDeQuantidadeDoNome.Ler(arquivo);

        lido.Nome.Should().Be(nomeEsperado);
        lido.Quantidade.Should().Be(qtdEsperada);
        lido.VeioDoNome.Should().BeTrue();
    }

    [Fact]
    public void Ler_MedidaComX_NaoConfundeComQuantidade()
    {
        var lido = LeitorDeQuantidadeDoNome.Ler("camisa 30x40");

        lido.Quantidade.Should().Be(1);
        lido.VeioDoNome.Should().BeFalse();
        lido.Nome.Should().Be("camisa 30x40");
    }

    [Fact]
    public void Ler_SemPadraoDeQuantidade_DevolveNomeOriginalEQuantidadeUm()
    {
        var lido = LeitorDeQuantidadeDoNome.Ler("manga");

        lido.Quantidade.Should().Be(1);
        lido.VeioDoNome.Should().BeFalse();
        lido.Nome.Should().Be("manga");
    }

    [Fact]
    public void Ler_SoSobraPontuacaoOuNumeroDeCopia_UsaONomeDoArquivoInteiro()
    {
        var lido = LeitorDeQuantidadeDoNome.Ler("5x (1)");

        lido.Quantidade.Should().Be(5);
        lido.Nome.Should().Be("5x (1)");
    }
}
