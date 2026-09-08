using System.Text.Json;
using OptimizePro.Core.Encaixe.Busca;
using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Services.Encaixe;

public sealed class EncaixeMemoriaService(IEncaixeMemoriaRepository repositorio) : IEncaixeMemoriaService
{
    // §12.1 — REDE_MINIMO_PARA_TREINAR / REDE_RETREINO_A_CADA / REDE_LIMIAR_MADUREZA / REDE_LIMIAR_DIVERSIDADE.
    private const int RedeMinimoParaTreinar = 30;
    private const int RedeRetreinoACada = 20;
    private const int RedeLimiarMadureza = 200;
    private const int RedeLimiarDiversidade = 20;

    public async Task<MemoriaDoTipo> ConsultarMemoriaAsync(string assinatura, CancellationToken ct = default)
    {
        var geral = await repositorio.SomarUsosEVitoriasPorReceitaAsync(ct);
        var doTipo = (await repositorio.ListarReceitasPorAssinaturaAsync(assinatura, ct))
            .ToDictionary(r => r.Receita, r => (r.Usos, r.Vitorias));

        // memoria[receita] = { usos: usosGeral + usosDoTipo*2, vitorias: vitoriasGeral*0.4 + vitoriasDoTipo*2 } (§12).
        var receitas = geral.ToDictionary(
            par => par.Key,
            par =>
            {
                var (usosGeral, vitoriasGeral) = par.Value;
                var (usosTipo, vitoriasTipo) = doTipo.TryGetValue(par.Key, out var t) ? t : (0, 0);
                return new PlacarDeMemoria(usosGeral + usosTipo * 2, vitoriasGeral * 0.4 + vitoriasTipo * 2);
            });

        var encaixesDoTipo = await repositorio.ContarHistoricoPorAssinaturaAsync(assinatura, ct);
        var encaixesNoTotal = await repositorio.ContarHistoricoTotalAsync(ct);
        var melhorAntes = await repositorio.MenorConsumoPorAssinaturaAsync(assinatura, ct);

        var redePesos = await repositorio.ObterRedePesosAsync(ct);
        var formatosDistintos = await repositorio.ContarAssinaturasDistintasParaTreinoAsync(ct);
        var redeExemplos = redePesos?.Exemplos ?? 0;
        var redeMadura = redeExemplos >= RedeLimiarMadureza && formatosDistintos >= RedeLimiarDiversidade;
        var rede = redePesos is null ? null : JsonSerializer.Deserialize<RedeNeural>(redePesos.PesosJson);

        return new MemoriaDoTipo(receitas, encaixesDoTipo, encaixesNoTotal, melhorAntes, redeMadura, redeExemplos, formatosDistintos, rede);
    }

    public async Task<int> RegistrarResultadoAsync(RegistroDeEncaixe registro, CancellationToken ct = default)
    {
        await repositorio.RegistrarUsoDeReceitaAsync(registro.Assinatura, registro.Receita, registro.Venceu, ct);

        await repositorio.RegistrarHistoricoAsync(new EncaixeHistorico
        {
            Assinatura = registro.Assinatura,
            LarguraTecido = registro.LarguraTecido,
            Pecas = registro.Pecas,
            Consumo = registro.Consumo,
            Aproveitamento = registro.Aproveitamento,
            Receita = registro.Receita,
            Tentativas = registro.Tentativas,
            CriadoEm = DateTime.UtcNow,
        }, ct);

        return await repositorio.ContarHistoricoPorAssinaturaAsync(registro.Assinatura, ct);
    }

    public async Task<int> RegistrarResultadoDaBuscaAsync(ResultadoDeBuscaParaMemoria resultado, CancellationToken ct = default)
    {
        // Cada linha do placar ganha usos+=1 incondicional; só a vencedora ganha vitorias+=1 (§12.1).
        foreach (var linha in resultado.Placar)
            await repositorio.RegistrarUsoDeReceitaAsync(resultado.Assinatura, linha.Receita, linha.Receita == resultado.ReceitaVencedora, ct);

        await repositorio.RegistrarHistoricoAsync(new EncaixeHistorico
        {
            Assinatura = resultado.Assinatura,
            LarguraTecido = resultado.LarguraTecido,
            Pecas = resultado.Pecas,
            Consumo = resultado.Consumo,
            Aproveitamento = resultado.Aproveitamento,
            Receita = resultado.ReceitaVencedora,
            Tentativas = resultado.Tentativas,
            CriadoEm = DateTime.UtcNow,
            FeaturesJson = JsonSerializer.Serialize(resultado.Features),
            PlacarJson = JsonSerializer.Serialize(resultado.Placar),
        }, ct);

        try
        {
            await TalvezRetreinarAsync(ct);
        }
        catch
        {
            // Falha de treino nunca derruba o salvamento do encaixe (§12.1 — "acelerador, não requisito").
        }

        return await repositorio.ContarHistoricoPorAssinaturaAsync(resultado.Assinatura, ct);
    }

    /// <summary>Porte de <c>talvezRetreinar</c> (§12.1) — reconstrói a rede do zero a partir do histórico inteiro, só quando há exemplos novos o bastante pra valer o custo.</summary>
    private async Task TalvezRetreinarAsync(CancellationToken ct)
    {
        var historico = await repositorio.ListarHistoricoParaTreinoAsync(ct);
        if (historico.Count < RedeMinimoParaTreinar) return;

        var pesosAtuais = await repositorio.ObterRedePesosAsync(ct);
        if (pesosAtuais is not null && historico.Count - pesosAtuais.Exemplos < RedeRetreinoACada) return;

        var exemplos = new List<RedeDeReceitas.ExemploDeTreino>();
        foreach (var h in historico)
        {
            var features = JsonSerializer.Deserialize<double[]>(h.FeaturesJson!)!;
            var placar = JsonSerializer.Deserialize<List<LinhaDoPlacarDto>>(h.PlacarJson!)!;

            foreach (var linha in placar)
            {
                var entrada = new double[features.Length + VocabularioDeReceita.Dimensao];
                features.CopyTo(entrada, 0);
                VocabularioDeReceita.VetorDaChave(linha.Receita).CopyTo(entrada, features.Length);

                // Alvo = 1 só pra receita que de fato venceu O ENCAIXE INTEIRO (h.Receita),
                // não "virou recorde em algum momento daquela fatia" (linha.Vitorias, §12.1 —
                // guia de melhorias 01/09/2026 §6.1: rótulo errado ensina a rede a reconhecer
                // "boa em algum instante", não "a melhor escolha", inflando falsos positivos.
                //
                // Alvo CONTÍNUO (02/09/2026, porte de melhoria do projeto de referência): a
                // campeã vale 1, as outras valem o quanto chegaram perto dela — não mais um
                // "1 ou 0" seco. Medido lá: histórico real tinha receitas rotuladas 0 mesmo
                // ficando 0,3% atrás da campeã, tratadas igual a uma que ficou 8% atrás — a
                // rede aprendia "venceu/não venceu" quando a pergunta certa é "quão perto".
                exemplos.Add(new RedeDeReceitas.ExemploDeTreino(entrada, AlvoDaReceita(linha, h.Receita, h.Consumo)));
            }
        }

        if (exemplos.Count == 0) return;

        var rede = RedeDeReceitas.CriarRede([RedeDeReceitas.DimensaoDeEntrada, 16, 8, 1], new Random());
        RedeDeReceitas.TreinarRede(rede, exemplos, new Random());

        await repositorio.SalvarRedePesosAsync(new EncaixeRedePesos
        {
            Id = 1,
            PesosJson = JsonSerializer.Serialize(rede),
            Exemplos = historico.Count,
            AtualizadoEm = DateTime.UtcNow,
        }, ct);
    }

    /// <summary>A partir de quanto atrás da campeã a receita vale zero (porte do projeto de referência) — 5% é bem mais que a diferença normal entre boas receitas de um mesmo trabalho, então quem passa disso realmente não serve pra ele.</summary>
    private const double AlvoTolerancia = 0.05;

    /// <summary>
    /// Alvo de treino contínuo (§12.1, porte de melhoria do projeto de referência): a campeã
    /// (<paramref name="receitaCampea"/>) vale 1; as outras valem o quanto <see cref="LinhaDoPlacarDto.MelhorConsumoCm"/>
    /// chegou perto do consumo dela, caindo linearmente até zerar aos 5% atrás. Linha gravada
    /// ANTES desta versão não tem <c>MelhorConsumoCm</c> (histórico antigo) — aí só sobra o que
    /// dá pra saber com certeza: não foi a campeã, alvo 0.
    /// </summary>
    private static double AlvoDaReceita(LinhaDoPlacarDto linha, string? receitaCampea, double? consumoCampeao)
    {
        if (linha.Receita == receitaCampea) return 1.0;

        var meu = linha.MelhorConsumoCm;
        if (meu is not { } m || !(m > 0) || !(consumoCampeao > 0)) return 0.0;

        var atras = (m - consumoCampeao.Value) / consumoCampeao.Value;
        if (atras <= 0) return 1.0; // empatou com a campeã (ou foi ainda melhor, por variação de ponto flutuante).
        return Math.Max(0.0, 1.0 - atras / AlvoTolerancia);
    }

    public async Task<EncaixeGuardadoDto?> BuscarGuardadoAsync(string chave, CancellationToken ct = default)
    {
        var g = await repositorio.ObterGuardadoAsync(chave, ct);
        return g is null ? null : ParaDto(g);
    }

    public async Task<bool> GuardarSeMelhorAsync(EncaixeGuardadoDto guardado, CancellationToken ct = default)
    {
        var existente = await repositorio.ObterGuardadoAsync(guardado.Chave, ct);
        if (existente is not null && guardado.Consumo >= existente.Consumo) return false;

        await repositorio.SalvarGuardadoAsync(new EncaixeGuardado
        {
            Chave = guardado.Chave,
            Assinatura = guardado.Assinatura,
            LarguraTecido = guardado.LarguraTecido,
            Espaco = guardado.Espaco,
            Margem = guardado.Margem,
            Consumo = guardado.Consumo,
            Aproveitamento = guardado.Aproveitamento,
            PecasJson = guardado.PecasJson,
            PosicoesJson = guardado.PosicoesJson,
            Receita = guardado.Receita,
            CriadoEm = existente?.CriadoEm ?? DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow,
        }, ct);

        return true;
    }

    public Task LimparMemoriaAsync(CancellationToken ct = default) => repositorio.LimparAsync(ct);

    private static EncaixeGuardadoDto ParaDto(EncaixeGuardado g) => new(
        g.Chave, g.Assinatura, g.LarguraTecido, g.Espaco, g.Margem, g.Consumo, g.Aproveitamento, g.PecasJson, g.PosicoesJson, g.Receita);
}
