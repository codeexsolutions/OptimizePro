namespace OptimizePro.Services.Projetos;

public sealed record ClienteResumo(int Id, string Nome, string? Observacoes, DateTime CriadoEm, DateTime? AtualizadoEm, int TotalProjetos);

public sealed record ClienteEntrada(string Nome, string? Observacoes);

public sealed record ProjetoResumo(
    int Id,
    string Nome,
    string? Observacoes,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    int TotalPecas,
    int PecasPorUnidade,
    string? Capa);

public sealed record ClienteComProjetos(int ClienteId, string ClienteNome, IReadOnlyList<ProjetoResumo> Projetos);

public sealed record ProjetoPecaDto(
    int Id,
    string Nome,
    string Arquivo,
    string Url,
    double Largura,
    double Altura,
    int Quantidade,
    int Ordem,
    string? Miniatura);

public sealed record ProjetoDetalhado(
    int Id,
    int ClienteId,
    string Nome,
    string? Observacoes,
    double? LarguraTecido,
    double? Espaco,
    double? Margem,
    string? Giro,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    IReadOnlyList<ProjetoPecaDto> Pecas);

public sealed record ProjetoPecaEntrada(
    string? Nome,
    string Arquivo,
    double Largura,
    double Altura,
    int Quantidade,
    int Ordem,
    string? Miniatura);

public sealed record ProjetoEntrada(
    string Nome,
    string? Observacoes,
    double? LarguraTecido,
    double? Espaco,
    double? Margem,
    string? Giro,
    IReadOnlyList<ProjetoPecaEntrada> Pecas);

public sealed record MiniaturaEntrada(int PecaId, string? Miniatura);
