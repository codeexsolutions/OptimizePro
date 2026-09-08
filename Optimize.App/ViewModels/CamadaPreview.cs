using Avalonia.Media;

namespace Optimize.App.ViewModels;

/// <summary>Uma camada (cor) do SVG gerado, pronta pra desenhar como <c>Avalonia.Controls.Shapes.Path</c> — a sintaxe de path do Avalonia é compatível com M/L/C/A/Z do SVG, então não precisa de biblioteca de SVG externa pra pré-visualizar o que o próprio <c>VetorService</c> gerou.</summary>
public sealed record CamadaPreview(Geometry Caminho, IBrush Cor);
