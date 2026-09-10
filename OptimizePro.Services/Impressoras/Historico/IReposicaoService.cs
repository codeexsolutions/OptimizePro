namespace OptimizePro.Services.Impressoras.Historico;

/// <summary>
/// Fachada de "Reposição" (§22.7, porte de <c>/api/impressoras/reposicao</c>) — trabalho
/// refeito, agrupado por semana (segunda a domingo). Reconhecido pela palavra "reposição" no
/// nome do arquivo, sem acento e sem caixa — não é um campo do sistema, é uma convenção da
/// produção que o sistema só lê. Por isso o número é um piso, não um total: reposição que
/// ninguém escreveu no nome não aparece aqui.
/// </summary>
public interface IReposicaoService
{
    Task<RespostaDeReposicao> ObterAsync(CancellationToken ct = default);
}
