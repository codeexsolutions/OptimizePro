using FluentAssertions;
using OptimizePro.Core.Encaixe.Busca;
using OptimizePro.Data.Repositorios;
using OptimizePro.Services.Encaixe;

namespace OptimizePro.Services.Tests;

public class EncaixeMemoriaServiceTests : IDisposable
{
    private readonly BancoDeTeste _banco = new();
    private readonly IEncaixeMemoriaRepository _repositorio;
    private readonly EncaixeMemoriaService _servico;

    public EncaixeMemoriaServiceTests()
    {
        _repositorio = new EncaixeMemoriaRepository(_banco.Db);
        _servico = new EncaixeMemoriaService(_repositorio);
    }

    public void Dispose() => _banco.Dispose();

    private static RegistroDeEncaixe Registro(string assinatura, string receita, bool venceu, double? consumo = null) =>
        new(assinatura, receita, venceu, LarguraTecido: 150, Pecas: 3, consumo, Aproveitamento: 80, Tentativas: 100);

    [Fact]
    public async Task ConsultarMemoria_SemHistorico_RetornaVazioComMelhorAntesNulo()
    {
        var memoria = await _servico.ConsultarMemoriaAsync("l15|1:2");

        memoria.Receitas.Should().BeEmpty();
        memoria.EncaixesDoTipo.Should().Be(0);
        memoria.EncaixesNoTotal.Should().Be(0);
        memoria.MelhorAntes.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarMemoria_CombinaCamadaGeralECamadaDoTipoComOsPesosDocumentados()
    {
        // camada geral: receita "R" usada por outra assinatura (2 usos, 1 vitória)
        await _servico.RegistrarResultadoAsync(Registro("outra-assinatura", "R", venceu: true));
        await _servico.RegistrarResultadoAsync(Registro("outra-assinatura", "R", venceu: false));

        // camada do tipo: mesma receita "R", mas para a assinatura consultada (1 uso, 1 vitória)
        await _servico.RegistrarResultadoAsync(Registro("minha-assinatura", "R", venceu: true));

        var memoria = await _servico.ConsultarMemoriaAsync("minha-assinatura");

        // "camada geral" soma TODAS as assinaturas (inclusive a consultada): usosGeral=2+1=3, vitoriasGeral=1+1=2.
        // usos = usosGeral(3) + usosDoTipo(1)*2 = 5; vitorias = vitoriasGeral(2)*0.4 + vitoriasDoTipo(1)*2 = 2.8
        memoria.Receitas["R"].Usos.Should().Be(5);
        memoria.Receitas["R"].Vitorias.Should().Be(2.8);
    }

    [Fact]
    public async Task RegistrarResultado_SempreInsereNoHistoricoERetornaEncaixesDoTipo()
    {
        var doTipo1 = await _servico.RegistrarResultadoAsync(Registro("A", "R1", true, consumo: 10));
        doTipo1.Should().Be(1);

        var doTipo2 = await _servico.RegistrarResultadoAsync(Registro("A", "R2", false, consumo: 12));
        doTipo2.Should().Be(2);

        var memoria = await _servico.ConsultarMemoriaAsync("A");
        memoria.EncaixesDoTipo.Should().Be(2);
        memoria.EncaixesNoTotal.Should().Be(2);
        memoria.MelhorAntes.Should().Be(10); // menor consumo do histórico dessa assinatura
    }

    [Fact]
    public async Task RegistrarResultado_ContaNoTotalMesmoParaOutraAssinatura()
    {
        await _servico.RegistrarResultadoAsync(Registro("A", "R1", true));
        await _servico.RegistrarResultadoAsync(Registro("B", "R1", true));

        var memoria = await _servico.ConsultarMemoriaAsync("A");
        memoria.EncaixesDoTipo.Should().Be(1);
        memoria.EncaixesNoTotal.Should().Be(2);
    }

    private static EncaixeGuardadoDto Guardado(string chave, double consumo, string? assinatura = "A") =>
        new(chave, assinatura, 150, 0.5, 1, consumo, 85, null, "[]", "receita-x");

    [Fact]
    public async Task GuardarSeMelhor_SemGuardadoAnterior_Guarda()
    {
        var guardou = await _servico.GuardarSeMelhorAsync(Guardado("chave-1", 10));

        guardou.Should().BeTrue();
        (await _servico.BuscarGuardadoAsync("chave-1"))!.Consumo.Should().Be(10);
    }

    [Fact]
    public async Task GuardarSeMelhor_ConsumoMenor_Substitui()
    {
        await _servico.GuardarSeMelhorAsync(Guardado("chave-1", 10));

        var guardou = await _servico.GuardarSeMelhorAsync(Guardado("chave-1", 8));

        guardou.Should().BeTrue();
        (await _servico.BuscarGuardadoAsync("chave-1"))!.Consumo.Should().Be(8);
    }

    [Fact]
    public async Task GuardarSeMelhor_ConsumoIgualOuMaior_NaoSubstitui()
    {
        await _servico.GuardarSeMelhorAsync(Guardado("chave-1", 10));

        var empateGuardou = await _servico.GuardarSeMelhorAsync(Guardado("chave-1", 10));
        var piorGuardou = await _servico.GuardarSeMelhorAsync(Guardado("chave-1", 12));

        empateGuardou.Should().BeFalse();
        piorGuardou.Should().BeFalse();
        (await _servico.BuscarGuardadoAsync("chave-1"))!.Consumo.Should().Be(10);
    }

    [Fact]
    public async Task BuscarGuardado_ChaveInexistente_RetornaNulo() =>
        (await _servico.BuscarGuardadoAsync("nao-existe")).Should().BeNull();

    private static ResultadoDeBuscaParaMemoria ResultadoDaBusca(
        string assinatura, string vencedora, IEnumerable<string> placarChaves, double[]? features = null) =>
        new(
            assinatura, vencedora,
            placarChaves.Select(r => new LinhaDoPlacarDto(r, 1, r == vencedora ? 1 : 0)).ToList(),
            features ?? new double[VetorizacaoDoTrabalho.Dimensao],
            LarguraTecido: 150, Pecas: 3, Consumo: 8.0, Aproveitamento: 80, Tentativas: 5);

    [Fact]
    public async Task RegistrarResultadoDaBusca_CadaLinhaGanhaUsoIncondicionalMasSoAVencedoraGanhaVitoria()
    {
        await _servico.RegistrarResultadoDaBuscaAsync(ResultadoDaBusca(
            "minha-assinatura", "contorno/solta/area/fundo", ["contorno/solta/area/fundo", "retangulo/empe/area/bl"]));

        var memoria = await _servico.ConsultarMemoriaAsync("minha-assinatura");

        // camada geral == camada do tipo aqui (só uma assinatura registrada): usos=1+1*2=3, vitorias=1*0.4+1*2=2.4 pra vencedora.
        memoria.Receitas["contorno/solta/area/fundo"].Usos.Should().Be(3);
        memoria.Receitas["contorno/solta/area/fundo"].Vitorias.Should().Be(2.4);

        // perdedora: usos+=1 (incondicional) mas vitorias fica em 0.
        memoria.Receitas["retangulo/empe/area/bl"].Usos.Should().Be(3);
        memoria.Receitas["retangulo/empe/area/bl"].Vitorias.Should().Be(0);
    }

    [Fact]
    public async Task TalvezRetreinar_RotulaPeloVencedorFinalDoEncaixe_NaoPorTerViradoRecordeMomentaneoNumaFatia()
    {
        // Cenário do bug (guia de melhorias 01/09/2026 §6.1): uma receita "decoy" vira recorde
        // MOMENTÂNEO dentro da própria fatia (linha.Vitorias=1) mas NUNCA é a vencedora real do
        // encaixe inteiro; a vencedora de verdade, por outro lado, é registrada com
        // linha.Vitorias=0 (pode acontecer — o placar por fatia não garante isso). O rótulo de
        // treino tem que seguir "quem venceu o encaixe" (h.Receita), não "linha.Vitorias>0".
        const string vencedora = "contorno/solta/area/fundo";
        const string decoy = "retangulo/empe/area/bl";
        var features = Enumerable.Range(0, VetorizacaoDoTrabalho.Dimensao).Select(i => (double)i / 20).ToArray();

        for (var i = 0; i < 30; i++)
        {
            var resultado = new ResultadoDeBuscaParaMemoria(
                $"assinatura-{i}", vencedora,
                [new LinhaDoPlacarDto(vencedora, 5, 0), new LinhaDoPlacarDto(decoy, 5, 1)],
                features, LarguraTecido: 150, Pecas: 3, Consumo: 8.0, Aproveitamento: 80, Tentativas: 10);

            await _servico.RegistrarResultadoDaBuscaAsync(resultado);
        }

        var memoria = await _servico.ConsultarMemoriaAsync("assinatura-0");
        memoria.Rede.Should().NotBeNull();

        double Pontuar(string chave)
        {
            var entrada = new double[VetorizacaoDoTrabalho.Dimensao + VocabularioDeReceita.Dimensao];
            features.CopyTo(entrada, 0);
            VocabularioDeReceita.VetorDaChave(chave).CopyTo(entrada, features.Length);
            return RedeDeReceitas.Prever(memoria.Rede!, entrada);
        }

        // Com o rótulo certo, a rede aprende que a VENCEDORA é boa aposta, mesmo nunca tendo
        // "virado recorde" localmente — e que a decoy é ruim, apesar do momento de destaque.
        Pontuar(vencedora).Should().BeGreaterThan(0.7);
        Pontuar(decoy).Should().BeLessThan(0.3);
    }

    [Fact]
    public async Task ConsultarMemoria_SemRedeTreinadaAinda_RedeNulaERedeMaduraFalso()
    {
        var memoria = await _servico.ConsultarMemoriaAsync("qualquer");

        memoria.Rede.Should().BeNull();
        memoria.RedeMadura.Should().BeFalse();
        memoria.RedeExemplos.Should().Be(0);
    }

    [Fact]
    public async Task RegistrarResultadoDaBusca_AoAcumularExemplosSuficientes_TreinaARedeAutomaticamente()
    {
        // REDE_MINIMO_PARA_TREINAR = 30 — cada chamada com placar de 1 linha soma 1 exemplo.
        for (var i = 0; i < 30; i++)
            await _servico.RegistrarResultadoDaBuscaAsync(ResultadoDaBusca($"assinatura-{i}", "contorno/solta/area/fundo", ["contorno/solta/area/fundo"]));

        var memoria = await _servico.ConsultarMemoriaAsync("assinatura-0");

        memoria.Rede.Should().NotBeNull();
        memoria.RedeExemplos.Should().Be(30);
        // ainda não madura: precisa de >=200 exemplos (REDE_LIMIAR_MADUREZA), não só >=30.
        memoria.RedeMadura.Should().BeFalse();
    }

    [Fact]
    public async Task RegistrarResultadoDaBusca_ComMenosDeTrintaExemplos_NaoTreina()
    {
        for (var i = 0; i < 29; i++)
            await _servico.RegistrarResultadoDaBuscaAsync(ResultadoDaBusca($"assinatura-{i}", "contorno/solta/area/fundo", ["contorno/solta/area/fundo"]));

        var memoria = await _servico.ConsultarMemoriaAsync("assinatura-0");

        memoria.Rede.Should().BeNull();
    }

    [Fact]
    public async Task LimparMemoria_ApagaReceitasHistoricoEGuardados()
    {
        await _servico.RegistrarResultadoAsync(Registro("A", "R1", true, consumo: 10));
        await _servico.GuardarSeMelhorAsync(Guardado("chave-1", 10));

        await _servico.LimparMemoriaAsync();

        var memoria = await _servico.ConsultarMemoriaAsync("A");
        memoria.Receitas.Should().BeEmpty();
        memoria.EncaixesNoTotal.Should().Be(0);
        (await _servico.BuscarGuardadoAsync("chave-1")).Should().BeNull();
    }
}
