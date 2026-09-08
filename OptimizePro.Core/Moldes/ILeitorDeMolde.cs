namespace OptimizePro.Core.Moldes;

public enum ModoLeituraVetor
{
    Marcador,
    Inteiro,
}

public sealed record OpcoesLeituraMolde(string? UnidadeForcada, ModoLeituraVetor Modo);

public sealed record ResultadoLeituraMolde(
    IReadOnlyList<PecaLida> Pecas,
    string Unidade,
    IReadOnlyList<string> Avisos,
    string? Erro);

public sealed record PecaLida(
    string Nome,
    IReadOnlyList<PontoXY> Contorno,
    IReadOnlyList<IReadOnlyList<PontoXY>> Furos,
    double LarguraCm,
    double AlturaCm);

/// <summary>
/// Um leitor por formato de arquivo de molde (DXF/PLT/SVG/PDF). Cada implementação
/// extrai <see cref="Traco"/>/<see cref="RotuloTexto"/> do arquivo e usa
/// <see cref="MontagemDeMoldes"/> para costurar/classificar em peças — ver §4.2 do
/// documento de arquitetura.
/// </summary>
public interface ILeitorDeMolde
{
    bool SuportaExtensao(string extensao);

    Task<ResultadoLeituraMolde> LerAsync(Stream conteudo, OpcoesLeituraMolde opcoes);
}
