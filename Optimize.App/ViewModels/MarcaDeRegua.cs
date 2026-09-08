namespace Optimize.App.ViewModels;

/// <summary>
/// Uma marca de régua no resultado do encaixe (§9.3, "identificar onde é o tecido") — posição
/// em pixels já convertida (mesma escala do <see cref="PosicaoVisual"/>) e o rótulo de medida
/// real (cm/m). <see cref="Maior"/> distingue a marca "cheia" (com rótulo, a cada 50cm) da
/// marca fina intermediária (a cada 10cm, só o traço).
/// </summary>
public sealed record MarcaDeRegua(double PosicaoPx, string Rotulo, bool Maior);
