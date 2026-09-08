using FluentAssertions;
using OptimizePro.Data.Repositorios;
using OptimizePro.Services.Armazenamento;
using OptimizePro.Services.Arquivos;
using OptimizePro.Services.Projetos;

namespace OptimizePro.Services.Tests;

public class ProjetoServiceTests : IDisposable
{
    private readonly BancoDeTeste _banco = new();
    private readonly string _pastaDados = Path.Combine(Path.GetTempPath(), "optimize-testes-" + Guid.NewGuid());
    private readonly IProjetoRepository _repositorio;
    private readonly CaminhosDoApp _caminhos;
    private readonly ProjetoService _servico;

    public ProjetoServiceTests()
    {
        _repositorio = new ProjetoRepository(_banco.Db);
        _caminhos = new CaminhosDoApp(_pastaDados);
        _servico = new ProjetoService(_repositorio, new ArquivoService(), _caminhos);
    }

    public void Dispose()
    {
        _banco.Dispose();
        if (Directory.Exists(_pastaDados)) Directory.Delete(_pastaDados, recursive: true);
    }

    private static ProjetoPecaEntrada Peca(string arquivo = "a.png", double largura = 10, double altura = 10, int quantidade = 1, int ordem = 0, string? miniatura = null) =>
        new("Peça", arquivo, largura, altura, quantidade, ordem, miniatura);

    [Fact]
    public async Task CriarCliente_NomeValido_Persiste()
    {
        var id = await _servico.CriarClienteAsync(new ClienteEntrada("Loja X", "obs"));

        var lista = await _servico.ListarClientesAsync();
        lista.Should().ContainSingle(c => c.Id == id && c.Nome == "Loja X" && c.TotalProjetos == 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CriarCliente_NomeVazio_LancaArgumentException(string nome)
    {
        var acao = () => _servico.CriarClienteAsync(new ClienteEntrada(nome, null));
        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CriarCliente_NomeAcimaDoLimite_LancaArgumentException()
    {
        var acao = () => _servico.CriarClienteAsync(new ClienteEntrada(new string('a', 121), null));
        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CriarCliente_ObservacoesAcimaDoLimite_LancaArgumentException()
    {
        var acao = () => _servico.CriarClienteAsync(new ClienteEntrada("X", new string('a', 501)));
        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AtualizarCliente_ClienteInexistente_LancaKeyNotFoundException()
    {
        var acao = () => _servico.AtualizarClienteAsync(999, new ClienteEntrada("X", null));
        await acao.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CriarProjeto_ApareceNaListaDoCliente()
    {
        var clienteId = await _servico.CriarClienteAsync(new ClienteEntrada("Loja X", null));
        var projetoId = await _servico.CriarProjetoAsync(clienteId, "Coleção verão");

        var comProjetos = await _servico.ListarProjetosDoClienteAsync(clienteId);

        comProjetos.Projetos.Should().ContainSingle(p => p.Id == projetoId && p.Nome == "Coleção verão");
    }

    [Fact]
    public async Task ListarProjetosDoCliente_ClienteInexistente_LancaKeyNotFoundException()
    {
        var acao = () => _servico.ListarProjetosDoClienteAsync(999);
        await acao.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AtualizarProjeto_SubstituiPecasECalculaResumo()
    {
        var clienteId = await _servico.CriarClienteAsync(new ClienteEntrada("Loja X", null));
        var projetoId = await _servico.CriarProjetoAsync(clienteId, "Projeto");

        await _servico.AtualizarProjetoAsync(projetoId, new ProjetoEntrada(
            "Projeto atualizado", "obs", 150, 2, 1, "180",
            [Peca(arquivo: "a.png", quantidade: 2, ordem: 0, miniatura: "data:x"), Peca(arquivo: "b.png", quantidade: 3, ordem: 1)]));

        var detalhado = await _servico.ObterProjetoAsync(projetoId);
        detalhado.Nome.Should().Be("Projeto atualizado");
        detalhado.LarguraTecido.Should().Be(150);
        detalhado.Pecas.Should().HaveCount(2);

        var resumo = (await _servico.ListarProjetosDoClienteAsync(clienteId)).Projetos.Single();
        resumo.TotalPecas.Should().Be(2);
        resumo.PecasPorUnidade.Should().Be(5); // 2 + 3
        resumo.Capa.Should().Be("data:x"); // miniatura da primeira peça (ordem 0)
    }

    [Fact]
    public async Task AtualizarProjeto_PecaComLarguraInvalida_EDescartada()
    {
        var clienteId = await _servico.CriarClienteAsync(new ClienteEntrada("Loja X", null));
        var projetoId = await _servico.CriarProjetoAsync(clienteId, "Projeto");

        await _servico.AtualizarProjetoAsync(projetoId, new ProjetoEntrada("Projeto", null, null, null, null, null,
            [Peca(largura: 0), Peca(arquivo: "ok.png")]));

        var detalhado = await _servico.ObterProjetoAsync(projetoId);
        detalhado.Pecas.Should().ContainSingle(p => p.Arquivo == "ok.png");
    }

    [Fact]
    public async Task AtualizarProjeto_ProjetoInexistente_LancaKeyNotFoundException()
    {
        var acao = () => _servico.AtualizarProjetoAsync(999, new ProjetoEntrada("X", null, null, null, null, null, []));
        await acao.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task PatchMiniaturasAsync_SoAtualizaPecasDoProjetoDentroDoLimite()
    {
        var clienteId = await _servico.CriarClienteAsync(new ClienteEntrada("Loja X", null));
        var projetoId = await _servico.CriarProjetoAsync(clienteId, "Projeto");
        await _servico.AtualizarProjetoAsync(projetoId, new ProjetoEntrada("Projeto", null, null, null, null, null, [Peca()]));

        var pecaId = (await _servico.ObterProjetoAsync(projetoId)).Pecas[0].Id;

        var guardadas = await _servico.PatchMiniaturasAsync(projetoId,
        [
            new MiniaturaEntrada(pecaId, "data:img"),
            new MiniaturaEntrada(999_999, "data:outra-peca-nao-existe"),
            new MiniaturaEntrada(pecaId, new string('a', 200_001)), // acima do limite, ignorada
        ]);

        guardadas.Should().Be(1);

        // ExecuteUpdateAsync não atualiza o change tracker do contexto original — relê com um
        // contexto novo, como aconteceria numa unidade de trabalho de verdade.
        using var contextoNovo = _banco.NovoContexto();
        var servicoNovo = new ProjetoService(new ProjetoRepository(contextoNovo), new ArquivoService(), _caminhos);
        var detalhado = await servicoNovo.ObterProjetoAsync(projetoId);
        detalhado.Pecas[0].Miniatura.Should().Be("data:img");
    }

    [Fact]
    public async Task ExcluirCliente_RemoveELimpaArquivosOrfaos()
    {
        var clienteId = await _servico.CriarClienteAsync(new ClienteEntrada("Loja X", null));
        var projetoId = await _servico.CriarProjetoAsync(clienteId, "Projeto");

        byte[] bytesPng = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A];
        var arquivo = await _servico.SalvarImagemDeProjetoAsync(bytesPng);
        await _servico.AtualizarProjetoAsync(projetoId, new ProjetoEntrada("Projeto", null, null, null, null, null, [Peca(arquivo: arquivo)]));

        var caminho = Path.Combine(_caminhos.PastaUploadsProjetos, arquivo);
        File.Exists(caminho).Should().BeTrue();

        await _servico.ExcluirClienteAsync(clienteId);

        File.Exists(caminho).Should().BeFalse();
        (await _servico.ListarClientesAsync()).Should().NotContain(c => c.Id == clienteId);
    }

    [Fact]
    public async Task SalvarImagemDeProjeto_AssinaturaDesconhecida_LancaArgumentException()
    {
        var acao = () => _servico.SalvarImagemDeProjetoAsync([0x00, 0x01]);
        await acao.Should().ThrowAsync<ArgumentException>();
    }
}
