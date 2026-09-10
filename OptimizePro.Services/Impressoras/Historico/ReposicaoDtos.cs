namespace OptimizePro.Services.Impressoras.Historico;

/// <summary>Porte dos tipos de <c>/api/impressoras/reposicao</c> (§22.7).</summary>
public sealed record ItemDeReposicao(string Id, string Data, string? Hora, string? NomeDaMaquina, string? Tarefa, double ComprimentoDeImpressao);

public sealed record SemanaDeReposicao(string InicioDaSemana, string FimDaSemana, double MetragemTotal, int Quantidade, List<ItemDeReposicao> Itens);

public sealed record RespostaDeReposicao(List<SemanaDeReposicao> Semanas, double MetragemTotal, int QuantidadeTotal);
