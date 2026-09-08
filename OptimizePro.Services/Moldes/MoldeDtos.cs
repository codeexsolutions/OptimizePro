using OptimizePro.Core;
using OptimizePro.Core.Arte;

namespace OptimizePro.Services.Moldes;

/// <summary>Os 12 papéis fixos sugeridos pela UI (§4.2/§8.3) — não é uma lista fechada de validação, "outro" cobre o resto.</summary>
public static class Papeis
{
    public static readonly IReadOnlyList<string> Lista =
    [
        "frente", "costas", "manga direita", "manga esquerda", "manga", "gola",
        "punho", "cós", "bolso", "vista", "forro", "outro",
    ];
}

public sealed record MoldeResumo(
    int Id,
    string Nome,
    string? Observacoes,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    IReadOnlyList<string> Tamanhos,
    int TotalPecas,
    int PecasPorUnidade);

public sealed record PecaDto(
    int Id,
    string Tamanho,
    string Papel,
    string? Nome,
    int Quantidade,
    double Largura,
    double Altura,
    List<PontoXY> Contorno,
    List<List<PontoXY>>? Furos,
    string? Origem,
    int Ordem);

public sealed record EstampaPecaDto(string Papel, string Arquivo, string Url, string? NomeOriginal, AjusteArte? Ajuste);

public sealed record EstampaDto(int Id, string Nome, IReadOnlyList<EstampaPecaDto> Pecas);

public sealed record MoldeDetalhado(
    int Id,
    string Nome,
    string? Observacoes,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    IReadOnlyList<PecaDto> Pecas,
    IReadOnlyList<EstampaDto> Artes);

public sealed record PecaEntrada(
    string? Tamanho,
    string Papel,
    string? Nome,
    int Quantidade,
    double Largura,
    double Altura,
    List<PontoXY> Contorno,
    List<List<PontoXY>>? Furos,
    string? Origem,
    int Ordem = 0);

public sealed record MoldeEntrada(string Nome, string? Observacoes, IReadOnlyList<PecaEntrada> Pecas);

public sealed record EstampaPecaEntrada(string Papel, string Arquivo, string? NomeOriginal, AjusteArte? Ajuste);

public sealed record EstampaEntrada(int? Id, string Nome, IReadOnlyList<EstampaPecaEntrada> Pecas);
