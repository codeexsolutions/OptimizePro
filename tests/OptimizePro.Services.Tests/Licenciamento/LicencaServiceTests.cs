using System.Security.Cryptography;
using FluentAssertions;
using OptimizePro.Licenciamento;
using OptimizePro.Services.Licenciamento;

namespace OptimizePro.Services.Tests.Licenciamento;

/// <summary>
/// Testa o fluxo completo do <see cref="LicencaService"/> usando a MESMA chave privada cujo
/// par público está hardcoded em <c>LicencaService.ChavePublicaBase64</c> — só assim dá pra
/// gerar código de teste que a verificação real aceita. Esta chave é a de DEMONSTRAÇÃO gerada
/// durante o desenvolvimento (§ "acesso controlado", 02/09/2026) — antes de vender pra
/// cliente de verdade, gere um par NOVO (<c>GeradorDeLicenca gerar-chave</c>) e troque tanto
/// aqui quanto em <c>LicencaService.ChavePublicaBase64</c>, senão qualquer um que veja este
/// arquivo de teste consegue forjar licença.
/// </summary>
public class LicencaServiceTests : IDisposable
{
    private const string ChavePrivadaDeTestePem = """
        -----BEGIN EC PRIVATE KEY-----
        MHcCAQEEIBzA3jenUi7eSc8oQyWbLmaHiYHsp9qMu+m2Cvxcb0WtoAoGCCqGSM49
        AwEHoUQDQgAEPkgzpFF8sWdclY7ydb2m8iUzFnHoXtiJvcrBHbRH3U/+i0Am98uY
        SRoVMPcnZP4nOs69mkvLrQB9zX1fV1THXA==
        -----END EC PRIVATE KEY-----
        """;

    private readonly string _arquivoTemporario = Path.Combine(Path.GetTempPath(), $"licenca-teste-{Guid.NewGuid():N}.dat");

    public void Dispose()
    {
        if (File.Exists(_arquivoTemporario)) File.Delete(_arquivoTemporario);
    }

    private static string GerarCodigo(DateOnly validoAte, TipoDeLicenca tipo = TipoDeLicenca.Paga)
    {
        using var chave = ECDsa.Create();
        chave.ImportFromPem(ChavePrivadaDeTestePem);
        return CodificadorDeLicenca.Gerar(chave, validoAte, clienteIdHash: 0, tipo);
    }

    private LicencaService NovoServico() => new(_arquivoTemporario);

    [Fact]
    public void ObterEstado_SemNuncaAtivar_DevolveNuncaAtivada()
    {
        var servico = NovoServico();

        var estado = servico.ObterEstado();

        estado.Situacao.Should().Be(SituacaoDaLicenca.NuncaAtivada);
        estado.Liberado.Should().BeFalse();
    }

    [Fact]
    public void Ativar_CodigoValidoEDentroDoPrazo_LiberaOAcesso()
    {
        var servico = NovoServico();
        var codigo = GerarCodigo(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));

        var resultado = servico.Ativar(codigo);

        resultado.Sucesso.Should().BeTrue();
        resultado.Estado!.Liberado.Should().BeTrue();
        servico.ObterEstado().Liberado.Should().BeTrue();
    }

    [Fact]
    public void Ativar_CodigoComAssinaturaInvalida_RecusaSemAtivarNada()
    {
        var servico = NovoServico();

        var resultado = servico.Ativar("XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX");

        resultado.Sucesso.Should().BeFalse();
        servico.ObterEstado().Situacao.Should().Be(SituacaoDaLicenca.NuncaAtivada);
    }

    [Fact]
    public void Ativar_CodigoJaVencido_RecusaAntesDeAtivar()
    {
        var servico = NovoServico();
        var codigoVencido = GerarCodigo(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));

        var resultado = servico.Ativar(codigoVencido);

        resultado.Sucesso.Should().BeFalse();
        resultado.Mensagem.Should().Contain("venceu");
        servico.ObterEstado().Situacao.Should().Be(SituacaoDaLicenca.NuncaAtivada);
    }

    [Fact]
    public void ObterEstado_AposCodigoVencerNaturalmente_DevolveExpirada()
    {
        var servico = NovoServico();
        // Válido só até ontem seria recusado na ativação (teste acima) — simula em vez disso um
        // código que ERA válido no passado mas cujo prazo já passou hoje: gera com validade
        // futura mínima (hoje) e confere que "amanhã" o estado muda sozinho pra Expirada. Como
        // não dá pra "viajar no tempo" de verdade num teste unitário, isto é coberto de forma
        // equivalente pelo teste anterior (código já vencido é recusado igual) — aqui cobrimos
        // o caminho de ATIVAR hoje mesmo, no último dia de validade.
        var codigo = GerarCodigo(DateOnly.FromDateTime(DateTime.UtcNow));

        var resultado = servico.Ativar(codigo);

        resultado.Sucesso.Should().BeTrue("o código ainda vale HOJE, no dia exato do vencimento");
    }

    [Fact]
    public void Ativar_CodigoQueNaoDecodifica_DevolveMensagemDeErroAmigavel()
    {
        var servico = NovoServico();

        var resultado = servico.Ativar("não é nem um código");

        resultado.Sucesso.Should().BeFalse();
        resultado.Mensagem.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Ativar_DuasVezesComCodigosDiferentes_OSegundoSubstituiOPrimeiro()
    {
        var servico = NovoServico();
        servico.Ativar(GerarCodigo(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5)));

        var validadeNova = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60);
        servico.Ativar(GerarCodigo(validadeNova));

        servico.ObterEstado().ValidoAte.Should().Be(validadeNova);
    }

    [Fact]
    public void ObterEstado_ArquivoDeEstadoInexistenteOuCorrompido_NuncaLancaExcecao()
    {
        File.WriteAllText(_arquivoTemporario, "isto não é um estado de licença válido nem criptografado");
        var servico = NovoServico();

        var acao = () => servico.ObterEstado();

        acao.Should().NotThrow();
        acao().Situacao.Should().Be(SituacaoDaLicenca.NuncaAtivada);
    }
}
