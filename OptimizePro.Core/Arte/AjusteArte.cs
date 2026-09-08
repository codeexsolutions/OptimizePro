namespace OptimizePro.Core.Arte;

public enum TipoArte { Arte, Rapport }

public enum ModoEncaixeArte { Cobrir, Caber, Esticar }

/// <summary>
/// Porte de <c>AjusteArte</c> de <c>public/arte-molde.js</c> (§9.1). <see cref="Modo"/> só
/// vale para <see cref="TipoArte.Arte"/>; para <see cref="TipoArte.Rapport"/> a arte entra
/// em tamanho real (via <see cref="PpcmArquivo"/>), repetida em ladrilho — nesse caso
/// <see cref="DeslocamentoXCm"/>/<see cref="DeslocamentoYCm"/> definem onde a repetição
/// começa, não uma posição centralizada.
/// </summary>
public sealed record AjusteArte(
    TipoArte Tipo,
    ModoEncaixeArte Modo,
    double EscalaPercentual,
    double DeslocamentoXCm,
    double DeslocamentoYCm,
    int GirauGraus,
    double? PpcmArquivo);
