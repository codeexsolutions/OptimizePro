using FluentAssertions;
using OptimizePro.Core;
using OptimizePro.Core.Arte;
using OptimizePro.Data.Repositorios;
using OptimizePro.Services.Armazenamento;
using OptimizePro.Services.Arquivos;
using OptimizePro.Services.Moldes;

namespace OptimizePro.Services.Tests;

public class MoldeServiceTests : IDisposable
{
    private readonly BancoDeTeste _banco = new();
    private readonly string _pastaDados = Path.Combine(Path.GetTempPath(), "optimize-testes-" + Guid.NewGuid());
    private readonly IMoldeRepository _repositorio;
    private readonly CaminhosDoApp _caminhos;
    private readonly MoldeService _servico;

    private static readonly List<PontoXY> ContornoValido = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];

    public MoldeServiceTests()
    {
        _repositorio = new MoldeRepository(_banco.Db);
        _caminhos = new CaminhosDoApp(_pastaDados);
        _servico = new MoldeService(_repositorio, new ArquivoService(), _caminhos);
    }

    public void Dispose()
    {
        _banco.Dispose();
        if (Directory.Exists(_pastaDados)) Directory.Delete(_pastaDados, recursive: true);
    }

    private static PecaEntrada Peca(string papel = "frente", string? tamanho = "único", int quantidade = 1, double largura = 10, double altura = 10, List<PontoXY>? contorno = null) =>
        new(tamanho, papel, null, quantidade, largura, altura, contorno ?? ContornoValido, null, null);

    [Fact]
    public async Task CriarAsync_PecaValida_PersisteEPermiteObter()
    {
        var id = await _servico.CriarAsync(new MoldeEntrada("Camiseta", null, [Peca()]));

        var detalhado = await _servico.ObterAsync(id);

        detalhado.Nome.Should().Be("Camiseta");
        detalhado.Pecas.Should().HaveCount(1);
        detalhado.Pecas[0].Contorno.Should().Equal(ContornoValido);
    }

    [Fact]
    public async Task CriarAsync_MisturaPecaValidaEInvalida_DescartaAInvalidaEMantemAValida()
    {
        var invalida = Peca(largura: 0);
        var id = await _servico.CriarAsync(new MoldeEntrada("Camiseta", null, [invalida, Peca()]));

        var detalhado = await _servico.ObterAsync(id);
        detalhado.Pecas.Should().HaveCount(1);
    }

    [Fact]
    public async Task CriarAsync_NenhumaPecaValida_LancaArgumentException()
    {
        var acao = () => _servico.CriarAsync(new MoldeEntrada("Vazio", null, [Peca(largura: 0)]));

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CriarAsync_ContornoComMenosDeTresPontos_PecaDescartada()
    {
        var acao = () => _servico.CriarAsync(new MoldeEntrada("Vazio", null, [Peca(contorno: [new(0, 0), new(1, 1)])]));

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ObterAsync_IdInexistente_LancaKeyNotFoundException()
    {
        var acao = () => _servico.ObterAsync(999);

        await acao.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AtualizarAsync_SubstituiPecasPorInteiro()
    {
        var id = await _servico.CriarAsync(new MoldeEntrada("Original", null, [Peca(papel: "frente")]));

        await _servico.AtualizarAsync(id, new MoldeEntrada("Atualizado", "obs", [Peca(papel: "costas"), Peca(papel: "manga")]));

        var detalhado = await _servico.ObterAsync(id);
        detalhado.Nome.Should().Be("Atualizado");
        detalhado.Observacoes.Should().Be("obs");
        detalhado.Pecas.Select(p => p.Papel).Should().BeEquivalentTo(["costas", "manga"]);
    }

    [Fact]
    public async Task AtualizarAsync_MoldeInexistente_LancaKeyNotFoundException()
    {
        var acao = () => _servico.AtualizarAsync(999, new MoldeEntrada("X", null, [Peca()]));

        await acao.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ListarAsync_CalculaTamanhosTotalPecasEPecasPorUnidade()
    {
        var id = await _servico.CriarAsync(new MoldeEntrada("Camiseta", null,
        [
            Peca(papel: "frente", tamanho: "P", quantidade: 2),
            Peca(papel: "costas", tamanho: "P", quantidade: 2),
            Peca(papel: "frente", tamanho: "M", quantidade: 3),
            Peca(papel: "costas", tamanho: "M", quantidade: 3),
        ]));

        var lista = await _servico.ListarAsync();
        var resumo = lista.Single(m => m.Id == id);

        resumo.Tamanhos.Should().BeEquivalentTo(["P", "M"]);
        resumo.TotalPecas.Should().Be(10);
        resumo.PecasPorUnidade.Should().Be(4);
    }

    [Fact]
    public async Task SalvarEstampaAsync_InsereENoisListarEstampasDevolve()
    {
        var moldeId = await _servico.CriarAsync(new MoldeEntrada("Camiseta", null, [Peca()]));

        var ajusteBruto = new AjusteArte(TipoArte.Arte, ModoEncaixeArte.Cobrir, 500, 0, 0, 47, null);
        var entrada = new EstampaEntrada(null, "Estampa floral",
        [
            new EstampaPecaEntrada("frente", "arte-1.png", "original.png", ajusteBruto),
        ]);

        var arteId = await _servico.SalvarEstampaAsync(moldeId, entrada);

        var estampas = await _servico.ListarEstampasAsync(moldeId);
        var estampa = estampas.Single(e => e.Id == arteId);

        estampa.Nome.Should().Be("Estampa floral");
        estampa.Pecas.Should().ContainSingle();
        estampa.Pecas[0].Ajuste!.EscalaPercentual.Should().Be(400); // clamp [10,400]
        estampa.Pecas[0].Ajuste!.GirauGraus.Should().Be(90); // 47 arredonda pro múltiplo de 90 mais próximo
    }

    [Fact]
    public async Task SalvarEstampaAsync_MoldeInexistente_LancaKeyNotFoundException()
    {
        var entrada = new EstampaEntrada(null, "X", [new EstampaPecaEntrada("frente", "a.png", null, null)]);

        var acao = () => _servico.SalvarEstampaAsync(999, entrada);

        await acao.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ExcluirAsync_RemoveMoldeELimpaArquivoDeArteOrfao()
    {
        var moldeId = await _servico.CriarAsync(new MoldeEntrada("Camiseta", null, [Peca()]));
        byte[] bytesPng = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A];
        var caminhoArquivo = await _servico.SalvarImagemDeArteAsync(moldeId, "frente", bytesPng, null);
        await _servico.SalvarEstampaAsync(moldeId, new EstampaEntrada(null, "Estampa", [new EstampaPecaEntrada("frente", caminhoArquivo, null, null)]));

        var caminhoCompleto = Path.Combine(_caminhos.PastaUploadsArtesMolde, caminhoArquivo);
        File.Exists(caminhoCompleto).Should().BeTrue();

        await _servico.ExcluirAsync(moldeId);

        File.Exists(caminhoCompleto).Should().BeFalse();
    }

    [Fact]
    public async Task ExcluirEstampaAsync_ArquivoAindaUsadoEmOutraEstampa_NaoApaga()
    {
        var moldeId = await _servico.CriarAsync(new MoldeEntrada("Camiseta", null, [Peca()]));
        await File.WriteAllBytesAsync(Path.Combine(_caminhos.PastaUploadsArtesMolde, "compartilhado.png"), [1]);

        var e1 = await _servico.SalvarEstampaAsync(moldeId, new EstampaEntrada(null, "E1", [new EstampaPecaEntrada("frente", "compartilhado.png", null, null)]));
        await _servico.SalvarEstampaAsync(moldeId, new EstampaEntrada(null, "E2", [new EstampaPecaEntrada("costas", "compartilhado.png", null, null)]));

        await _servico.ExcluirEstampaAsync(moldeId, e1);

        File.Exists(Path.Combine(_caminhos.PastaUploadsArtesMolde, "compartilhado.png")).Should().BeTrue();
    }
}
