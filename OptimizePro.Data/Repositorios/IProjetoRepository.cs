using OptimizePro.Data.Entidades;

namespace OptimizePro.Data.Repositorios;

/// <summary>Projeção leve de <c>projeto_clientes</c> com a contagem de projetos (LEFT JOIN, §4.3).</summary>
public sealed record ClienteComContagem(int Id, string Nome, string? Observacoes, DateTime CriadoEm, DateTime? AtualizadoEm, int TotalProjetos);

/// <summary>Espelha as rotas de <c>projetos-api.js</c> (§4.3 da especificação): CRUD de cliente/projeto/peça, incluindo a regra de miniatura.</summary>
public interface IProjetoRepository
{
    Task<List<ClienteComContagem>> ListarClientesComContagemAsync(CancellationToken ct = default);
    Task<ProjetoCliente?> ObterClienteAsync(int id, CancellationToken ct = default);

    /// <summary>Cliente + projetos, cada um com as peças carregadas (usado pra montar capa/contagens, §4.3).</summary>
    Task<ProjetoCliente?> ObterClienteComProjetosEPecasAsync(int id, CancellationToken ct = default);

    Task<int> CriarClienteAsync(ProjetoCliente cliente, CancellationToken ct = default);

    /// <summary>Retorna false se o cliente não existir.</summary>
    Task<bool> AtualizarClienteAsync(int id, string nome, string? observacoes, DateTime atualizadoEm, CancellationToken ct = default);

    Task ExcluirClienteAsync(int id, CancellationToken ct = default);

    Task<Projeto?> ObterProjetoAsync(int id, CancellationToken ct = default);
    Task<int> CriarProjetoAsync(Projeto projeto, CancellationToken ct = default);

    /// <summary>Substitui os campos e o conjunto inteiro de peças (delete+reinsert, §4.3). Retorna false se o projeto não existir.</summary>
    Task<bool> AtualizarProjetoAsync(
        int id, string nome, string? observacoes, double? larguraTecido, double? espaco, double? margem, string? giro,
        List<ProjetoPeca> pecas, DateTime atualizadoEm, CancellationToken ct = default);

    Task ExcluirProjetoAsync(int id, CancellationToken ct = default);

    /// <summary>Grava só a coluna miniatura, sem tocar no resto da peça (§4.3 da especificação).</summary>
    Task PatchMiniaturaAsync(int pecaId, string? miniatura, CancellationToken ct = default);

    /// <summary>Ids de peças que pertencem a este projeto — usado pra validar o payload de PATCH miniaturas antes de gravar.</summary>
    Task<HashSet<int>> ListarIdsDePecasAsync(int projetoId, CancellationToken ct = default);

    /// <summary>Todos os nomes de arquivo de projeto referenciados no banco inteiro — usado pela faxina de órfãos (§6, regra crítica: conferir contra a tabela inteira).</summary>
    Task<List<string>> ListarTodosArquivosDeProjetoAsync(CancellationToken ct = default);
}
