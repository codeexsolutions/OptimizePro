using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Services.Impressoras;

/// <summary>Uma imagem anexada na criação de uma OS (§22.9) — o que a tela manda, antes de virar <see cref="OrdemDeServicoImagem"/>.</summary>
public sealed record ImagemParaOrdemDeServico(string NomeDoArquivo, string TipoMime, byte[] Dados);

/// <summary>Fachada de Ordens de Serviço (§22.9) — tela completa (criar+excluir), decisão do usuário; diferente da referência, que só guarda o modelo de dados.</summary>
public interface IOrdemDeServicoService
{
    Task<string> CriarAsync(
        string nomeDoCliente, string? tecido, string? tamanhoDeImpressao, double? metros,
        string? operador, string? maquina, string data, string? observacao,
        IReadOnlyList<ImagemParaOrdemDeServico> imagens, CancellationToken ct = default);

    Task<List<ResumoDeOrdemDeServico>> ListarAsync(string? busca = null, CancellationToken ct = default);

    Task<OrdemDeServico?> ObterAsync(string id, CancellationToken ct = default);

    Task<bool> ExcluirAsync(string id, CancellationToken ct = default);
}
