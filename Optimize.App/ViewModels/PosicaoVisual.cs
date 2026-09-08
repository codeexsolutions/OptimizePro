using Avalonia.Media;

namespace Optimize.App.ViewModels;

/// <summary>
/// Uma peça posicionada no resultado do encaixe, já convertida pra pixels de tela (§10.1) —
/// <see cref="Contorno"/> é a geometria real da peça (rotacionada/deslocada em coordenadas
/// absolutas do canvas), não mais um retângulo delimitador: desenha a arte de verdade, igual
/// ao original. É um <see cref="Geometry"/> pronto (não <c>Points</c> cru) pela mesma razão já
/// documentada em <c>EncaixeViewModel</c>/<c>VetorViewModel</c>: bindings de string/coleção
/// crua pra forma são frágeis em Avalonia — um objeto real construído na ViewModel não é.
/// </summary>
public sealed record PosicaoVisual(Geometry Contorno, double CentroX, double CentroY, IBrush Cor, string Rotulo);
