namespace OptimizePro.Core.Moldes.Plt;

/// <summary>Um comando HP-GL tokenizado: mnemônico de 2 letras + parâmetros.</summary>
/// <param name="Opcode">Mnemônico em maiúsculas (ex.: "PU", "AA", "LB").</param>
/// <param name="Dados">Texto cru para LB/PE (que não são listas numéricas comuns); null para os demais.</param>
/// <param name="Numeros">Parâmetros numéricos separados por vírgula, para comandos comuns.</param>
public sealed record PltComando(string Opcode, string? Dados, IReadOnlyList<double> Numeros);
