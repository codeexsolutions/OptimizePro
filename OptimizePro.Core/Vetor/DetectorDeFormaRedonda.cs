namespace OptimizePro.Core.Vetor;

/// <summary>
/// Porte de <c>acharFormaRedonda</c> (§14.5) — testa se um contorno é (aproximadamente)
/// um círculo, com duas peneiras: erro máximo do ajuste de Kåsa dentro da tolerância, E
/// área do contorno batendo com <c>π*r²</c> (±6%) — a área pega casos que o erro sozinho
/// deixaria passar (ex.: um quadrado tem erro zero contra o círculo circunscrito nos
/// próprios 4 cantos, mas a área não bate nem perto).
/// </summary>
/// <remarks>
/// Só detecta círculo — a especificação também cita elipse na opção "redondas", mas a
/// fórmula dada (Kåsa) é só para círculo; ajuste de elipse geral fica para depois se
/// peças ovais reais precisarem disso.
/// </remarks>
public static class DetectorDeFormaRedonda
{
    public const double ToleranciaDeAreaPadrao = 0.06;

    /// <summary>
    /// Erro máximo do ajuste, além do absoluto, também não pode passar desta FRAÇÃO do raio
    /// ajustado (02/09/2026, achado com manchas circulares falsas aparecendo em formas
    /// pequenas — letras, detalhes finos). O piso absoluto de tolerância (ex.:
    /// <c>Math.Max(0.8, ...)</c> em <c>VetorService</c>) é generoso demais pra um contorno
    /// pequeno: 0,8px de erro num círculo de raio 3px é 27% de erro relativo — folgado o
    /// bastante pra um blob qualquer, sem nada de circular, passar no teste por acidente.
    /// </summary>
    public const double ToleranciaDeErroRelativaAoRaio = 0.15;

    public static CirculoAjustado? Detectar(IReadOnlyList<PontoXY> pontosBrutos, double toleranciaDeErro, double toleranciaDeArea = ToleranciaDeAreaPadrao)
    {
        var ajuste = AjusteDeCirculo.Ajustar(pontosBrutos);
        if (ajuste is not { } circulo)
            return null;

        var toleranciaEfetiva = Math.Min(toleranciaDeErro, circulo.Raio * ToleranciaDeErroRelativaAoRaio);
        var erroMaximo = pontosBrutos.Max(p => Math.Abs(Geometria.DistanciaEntre(p, circulo.Centro) - circulo.Raio));
        if (erroMaximo > toleranciaEfetiva)
            return null;

        var areaDoContorno = Math.Abs(Geometria.AreaComSinal(pontosBrutos));
        var areaDoCirculo = Math.PI * circulo.Raio * circulo.Raio;

        if (areaDoCirculo <= 0)
            return null;

        var diferencaRelativa = Math.Abs(areaDoContorno - areaDoCirculo) / areaDoCirculo;
        if (diferencaRelativa > toleranciaDeArea)
            return null;

        return circulo;
    }
}
