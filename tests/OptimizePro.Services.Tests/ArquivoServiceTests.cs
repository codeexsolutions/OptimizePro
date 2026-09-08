using FluentAssertions;
using OptimizePro.Services.Arquivos;

namespace OptimizePro.Services.Tests;

public class ArquivoServiceTests
{
    private readonly ArquivoService _servico = new();

    [Theory]
    [InlineData(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A }, "png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "jpg")]
    [InlineData(new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' }, "gif")]
    public void DetectarExtensao_AssinaturaConhecida_RetornaExtensaoCorreta(byte[] bytes, string esperado) =>
        _servico.DetectarExtensao(bytes).Should().Be(esperado);

    [Fact]
    public void DetectarExtensao_Webp_RetornaWebp()
    {
        // RIFF....WEBP
        byte[] bytes = [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P'];
        _servico.DetectarExtensao(bytes).Should().Be("webp");
    }

    [Fact]
    public void DetectarExtensao_AssinaturaDesconhecida_RetornaNulo() =>
        _servico.DetectarExtensao([0x00, 0x01, 0x02, 0x03]).Should().BeNull();

    [Fact]
    public void GerarNomeSemColisao_SeguePrefixoTimestampSufixoExtensao()
    {
        var nome = _servico.GerarNomeSemColisao("arte", "png");

        nome.Should().MatchRegex(@"^arte-\d+-[0-9a-z]{6}\.png$");
    }

    [Fact]
    public void GerarNomeSemColisao_DuasChamadas_NuncaColidem()
    {
        var nomes = Enumerable.Range(0, 50).Select(_ => _servico.GerarNomeSemColisao("x", "png")).ToList();
        nomes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SalvarAsync_GravaOsBytesNoCaminhoEsperado()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "optimize-testes-" + Guid.NewGuid());
        try
        {
            var bytes = new byte[] { 1, 2, 3, 4 };
            var caminho = await _servico.SalvarAsync(pasta, "arquivo.bin", bytes);

            File.Exists(caminho).Should().BeTrue();
            (await File.ReadAllBytesAsync(caminho)).Should().Equal(bytes);
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public void LimparOrfaos_ApagaSoOsCandidatosQueNaoEstaoEmUso()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "optimize-testes-" + Guid.NewGuid());
        Directory.CreateDirectory(pasta);
        try
        {
            var emUsoPath = Path.Combine(pasta, "em-uso.png");
            var orfaoPath = Path.Combine(pasta, "orfao.png");
            File.WriteAllBytes(emUsoPath, [1]);
            File.WriteAllBytes(orfaoPath, [1]);

            _servico.LimparOrfaos(pasta, arquivosEmUso: ["em-uso.png"], candidatosARemover: ["em-uso.png", "orfao.png"]);

            File.Exists(emUsoPath).Should().BeTrue();
            File.Exists(orfaoPath).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public void LimparOrfaos_ArquivoJaRemovido_NaoLancaExcecao()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "optimize-testes-" + Guid.NewGuid());

        var acao = () => _servico.LimparOrfaos(pasta, arquivosEmUso: [], candidatosARemover: ["inexistente.png"]);

        acao.Should().NotThrow();
    }
}
