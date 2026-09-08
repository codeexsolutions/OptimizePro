using OptimizePro.Core.Encaixe.Busca;

namespace OptimizePro.Services.Encaixe;

/// <summary>Placar combinado de uma receita (§11.12/§12): duas camadas — geral (todas as assinaturas, peso 0.4×/1×) e do tipo exato (peso 2×).</summary>
public sealed record PlacarDeMemoria(double Usos, double Vitorias);

/// <summary>
/// Resultado de <see cref="IEncaixeMemoriaService.ConsultarMemoriaAsync"/> — as duas camadas de
/// sempre (§12) mais o estado da rede das receitas (§12.1, camada opcional por cima).
/// </summary>
public sealed record MemoriaDoTipo(
    IReadOnlyDictionary<string, PlacarDeMemoria> Receitas,
    int EncaixesDoTipo,
    int EncaixesNoTotal,
    double? MelhorAntes,
    bool RedeMadura,
    int RedeExemplos,
    int RedeFormatosDistintos,
    RedeNeural? Rede);

/// <summary>Porte do payload de <c>POST /api/encaixe/memoria</c> (§4.4) — <c>Venceu</c> corresponde ao <c>placar</c> da rota original.</summary>
public sealed record RegistroDeEncaixe(
    string Assinatura,
    string Receita,
    bool Venceu,
    double? LarguraTecido,
    int? Pecas,
    double? Consumo,
    double? Aproveitamento,
    int? Tentativas);

/// <summary>
/// Uma linha do placar de uma busca inteira (§11.7/§12.1) — quantas vezes esta receita foi
/// tentada, e quantas vezes virou o recorde corrente daquela busca. <see cref="MelhorConsumoCm"/>
/// (02/09/2026, porte de melhoria do projeto de referência) é o melhor consumo que ESTA receita
/// conseguiu sozinha — alimenta o alvo de treino contínuo da rede (§12.1). Opcional/nulo pra não
/// quebrar histórico já gravado antes desta versão nem os testes que só olham vitória/derrota.
/// </summary>
public sealed record LinhaDoPlacarDto(string Receita, int Tentativas, int Vitorias, double? MelhorConsumoCm = null);

/// <summary>
/// Porte do payload real de <c>POST /api/encaixe/memoria</c> quando a busca inteira (não só uma
/// receita) é registrada de uma vez (§11.12/§12.1) — alimenta tanto a memória por balde exato
/// (uma linha de <see cref="Placar"/> por vez, via <see cref="IEncaixeMemoriaService.RegistrarResultadoAsync"/>)
/// quanto o treino da rede das receitas (<see cref="Features"/>/<see cref="Placar"/> gravados
/// crus no histórico, §12.1 <c>montarExemplosDeTreino</c>).
/// </summary>
public sealed record ResultadoDeBuscaParaMemoria(
    string Assinatura,
    string ReceitaVencedora,
    IReadOnlyList<LinhaDoPlacarDto> Placar,
    IReadOnlyList<double> Features,
    double? LarguraTecido,
    int? Pecas,
    double? Consumo,
    double? Aproveitamento,
    int? Tentativas);

/// <summary>Encaixe físico completo (posição de cada peça) salvo pra um trabalho exato — §11.12/§12.</summary>
public sealed record EncaixeGuardadoDto(
    string Chave,
    string? Assinatura,
    double? LarguraTecido,
    double? Espaco,
    double? Margem,
    double Consumo,
    double? Aproveitamento,
    string? PecasJson,
    string PosicoesJson,
    string? Receita);
