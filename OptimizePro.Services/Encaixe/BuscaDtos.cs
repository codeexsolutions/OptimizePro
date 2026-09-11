using OptimizePro.Core;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Services.Encaixe;

/// <summary>Uma peça a encaixar, antes de expandir em itens (§11.1 — "peças expandem em itens; 1 cópia cada").</summary>
public sealed record PecaParaEncaixar(string Id, IReadOnlyList<PontoXY> Contorno, int Quantidade, TipoDeGiro Giro);

/// <summary>"Como encaixar" (tela de Encaixe) — automático deixa contorno e retângulo disputarem; forçar um só é escolha explícita do usuário.</summary>
public enum ModoDeEncaixe { Automatico, SempreContorno, SempreCaixa }

public sealed record ConfiguracaoDeEncaixe(
    double LarguraTecidoCm,
    double EspacoCm,
    double MargemCm,
    long TempoMaximoMs = 8_000,
    long MsSemGanhoParaParedeMs = 1_500,
    ModoDeEncaixe Modo = ModoDeEncaixe.Automatico,
    /// <summary>"Bancada" (porte de <c>encaixeMotor.js</c>) — comprimento máximo do rolo em cm; nenhuma peça cruza essa linha. <c>null</c> = sem limite, o comportamento de sempre.</summary>
    double? ComprimentoBancadaCm = null);

public sealed record ItemDeResultado(string PecaId, double X, double Y, double LarguraCm, double AlturaCm, int RotacaoGraus);

public sealed record ResultadoDeEncaixe(
    IReadOnlyList<ItemDeResultado> Posicoes,
    IReadOnlyList<string> ItensNaoEncaixados,
    double ConsumoCm,
    double AproveitamentoPercentual,
    int Tentativas,
    string ReceitaVencedora,
    double AreaRealCm2);

public sealed record AndamentoDoEncaixe(int Tentativas, double MelhorConsumoCm, long TempoDecorridoMs);
