using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using OptimizePro.Core.Impressoras;
using OptimizePro.Data.Entidades;
using OptimizePro.Licenciamento;
using OptimizePro.Painel;
using OptimizePro.Services.Armazenamento;
using OptimizePro.Services.Licenciamento;

namespace OptimizePro.Sincronizacao.Tests;

/// <summary>Mesma chave de demonstração usada em <c>LicencaServiceTests</c> — ver o aviso lá sobre trocar antes de vender de verdade.</summary>
public class SincronizacaoServiceTests : IDisposable
{
    private const string ChavePrivadaDeTestePem = """
        -----BEGIN EC PRIVATE KEY-----
        MHcCAQEEIBzA3jenUi7eSc8oQyWbLmaHiYHsp9qMu+m2Cvxcb0WtoAoGCCqGSM49
        AwEHoUQDQgAEPkgzpFF8sWdclY7ydb2m8iUzFnHoXtiJvcrBHbRH3U/+i0Am98uY
        SRoVMPcnZP4nOs69mkvLrQB9zX1fV1THXA==
        -----END EC PRIVATE KEY-----
        """;

    private readonly string _pastaTemporaria = Path.Combine(Path.GetTempPath(), $"sincronizacao-teste-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_pastaTemporaria)) Directory.Delete(_pastaTemporaria, recursive: true);
    }

    private static string GerarCodigoDeLicenca(uint clienteIdHash)
    {
        using var chave = ECDsa.Create();
        chave.ImportFromPem(ChavePrivadaDeTestePem);
        return CodificadorDeLicenca.Gerar(chave, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), clienteIdHash, TipoDeLicenca.Paga);
    }

    private LicencaService NovaLicencaAtivada(uint clienteIdHash)
    {
        var caminhos = new CaminhosDoApp(_pastaTemporaria);
        var licenca = new LicencaService(caminhos);
        licenca.Ativar(GerarCodigoDeLicenca(clienteIdHash)).Sucesso.Should().BeTrue();
        return licenca;
    }

    private ArmazenamentoDeSincronizacao NovoArmazenamento() =>
        new(new CaminhosDoApp(_pastaTemporaria));

    [Fact]
    public async Task Sincronizar_SemLicencaAtivada_NaoProvisionaNemEnviaNada()
    {
        using var banco = new BancoDeTeste();
        var caminhos = new CaminhosDoApp(_pastaTemporaria);
        var licenca = new LicencaService(caminhos); // nunca ativada
        var cliente = new ClienteCentralFalso();
        var servico = new SincronizacaoService(banco.Operacional, banco.Painel, licenca, NovoArmazenamento(), cliente);

        var resultado = await servico.SincronizarAsync();

        resultado.Should().BeFalse();
        cliente.ChamadasDeProvisionar.Should().Be(0);
        cliente.ChamadasDeEnvio.Should().Be(0);
    }

    [Fact]
    public async Task Sincronizar_ClienteNaoConfigurado_NaoTentaNada()
    {
        using var banco = new BancoDeTeste();
        var licenca = NovaLicencaAtivada(111u);
        var cliente = new ClienteCentralFalso { Configurado = false };
        var servico = new SincronizacaoService(banco.Operacional, banco.Painel, licenca, NovoArmazenamento(), cliente);

        (await servico.SincronizarAsync()).Should().BeFalse();
        cliente.ChamadasDeProvisionar.Should().Be(0);
    }

    [Fact]
    public async Task Sincronizar_PrimeiraVez_ProvisionaESalvaOEstadoLocal()
    {
        using var banco = new BancoDeTeste();
        var licenca = NovaLicencaAtivada(222u);
        var cliente = new ClienteCentralFalso
        {
            ProximaRespostaDeProvisionamento = new RespostaDeProvisionamento("inst-1", "chave-nova", JaExistia: false),
        };
        var armazenamento = NovoArmazenamento();
        var servico = new SincronizacaoService(banco.Operacional, banco.Painel, licenca, armazenamento, cliente);

        await servico.SincronizarAsync();

        cliente.ChamadasDeProvisionar.Should().Be(1);
        var estadoSalvo = armazenamento.Ler();
        estadoSalvo.Should().Be(new EstadoLocalDeSincronizacao("inst-1", "chave-nova"));
    }

    [Fact]
    public async Task Sincronizar_JaProvisionado_NaoProvisionaDeNovo()
    {
        using var banco = new BancoDeTeste();
        banco.Operacional.Maquinas.Add(new Maquina { Id = "m1", Nome = "Impressora 1", Tipo = TipoDeMaquina.Csv });
        await banco.Operacional.SaveChangesAsync();

        var licenca = NovaLicencaAtivada(333u);
        var armazenamento = NovoArmazenamento();
        armazenamento.Salvar(new EstadoLocalDeSincronizacao("inst-existente", "chave-existente"));
        var cliente = new ClienteCentralFalso();
        var servico = new SincronizacaoService(banco.Operacional, banco.Painel, licenca, armazenamento, cliente);

        await servico.SincronizarAsync();

        cliente.ChamadasDeProvisionar.Should().Be(0);
        cliente.UltimasCredenciaisUsadas.Should().Be(("inst-existente", "chave-existente"));
    }

    [Fact]
    public async Task Sincronizar_ProvisionamentoSemChaveNova_NaoEnviaNada()
    {
        using var banco = new BancoDeTeste();
        var licenca = NovaLicencaAtivada(444u);
        var cliente = new ClienteCentralFalso
        {
            ProximaRespostaDeProvisionamento = new RespostaDeProvisionamento("inst-1", null, JaExistia: true),
        };
        var servico = new SincronizacaoService(banco.Operacional, banco.Painel, licenca, NovoArmazenamento(), cliente);

        var resultado = await servico.SincronizarAsync();

        resultado.Should().BeFalse();
        cliente.ChamadasDeEnvio.Should().Be(0);
    }

    [Fact]
    public async Task Sincronizar_ComDadosLocais_EnviaMaquinasHistoricoPedidosOsUsuariosEFaturamento()
    {
        using var banco = new BancoDeTeste();

        banco.Operacional.Maquinas.Add(new Maquina { Id = "m1", Nome = "Impressora 1", Tipo = TipoDeMaquina.Csv });
        banco.Operacional.RegistrosDeImpressao.Add(new RegistroDeImpressao
        {
            Id = "r1", MaquinaId = "m1", DataHora = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            Data = DateTime.UtcNow.ToString("yyyy-MM-dd"), Tarefa = "Cliente - Tecido.prt",
        });
        banco.Operacional.Pedidos.Add(new Pedido
        {
            Id = "p1", Status = "aberto",
            Itens = [new PedidoItem { Id = "pi1", PedidoId = "p1", RegistroId = "r1", Posicao = 0 }],
        });
        banco.Operacional.OrdensDeServico.Add(new OrdemDeServico { Id = "os1", NomeDoCliente = "Cliente A", Data = "2026-01-01" });
        await banco.Operacional.SaveChangesAsync();

        banco.Painel.Usuarios.Add(new Usuario
        {
            Id = "u1", Login = "dono", Nome = "Dono", SenhaHash = [1, 2, 3], SenhaSal = [4, 5, 6],
            ModulosLiberados = [ModuloDoPainel.Historico],
        });
        banco.Painel.ConfiguracoesDeFaturamento.Add(new ConfiguracaoDeFaturamento { ValorBaseMensal = 300m, ValorPorUsuarioExtra = 25m });
        await banco.Painel.SaveChangesAsync();

        var licenca = NovaLicencaAtivada(555u);
        var armazenamento = NovoArmazenamento();
        armazenamento.Salvar(new EstadoLocalDeSincronizacao("inst-1", "chave-1"));
        var cliente = new ClienteCentralFalso();
        var servico = new SincronizacaoService(banco.Operacional, banco.Painel, licenca, armazenamento, cliente);

        var resultado = await servico.SincronizarAsync();

        resultado.Should().BeTrue();
        cliente.ChamadasDeEnvio.Should().Be(1);
        var tipos = cliente.UltimoLoteEnviado!.Select(i => i.Tipo).ToList();
        tipos.Should().Contain([
            TipoDeItem.Maquina, TipoDeItem.RegistroDeImpressao, TipoDeItem.Pedido,
            TipoDeItem.OrdemDeServico, TipoDeItem.Usuario, TipoDeItem.Faturamento,
        ]);

        var itemDoPedido = cliente.UltimoLoteEnviado!.Single(i => i.Tipo == TipoDeItem.Pedido);
        using var pedidoJson = JsonDocument.Parse(itemDoPedido.DadosJson);
        pedidoJson.RootElement.GetProperty("Itens").GetArrayLength().Should().Be(1, "o pedido deve levar os itens junto");
    }
}
