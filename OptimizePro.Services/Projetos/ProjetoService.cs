using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;
using OptimizePro.Services.Armazenamento;
using OptimizePro.Services.Arquivos;

namespace OptimizePro.Services.Projetos;

public sealed class ProjetoService(IProjetoRepository repositorio, IArquivoService arquivos, CaminhosDoApp caminhos) : IProjetoService
{
    public async Task<IReadOnlyList<ClienteResumo>> ListarClientesAsync(CancellationToken ct = default)
    {
        var clientes = await repositorio.ListarClientesComContagemAsync(ct);
        return clientes.Select(c => new ClienteResumo(c.Id, c.Nome, c.Observacoes, c.CriadoEm, c.AtualizadoEm, c.TotalProjetos)).ToList();
    }

    public async Task<int> CriarClienteAsync(ClienteEntrada entrada, CancellationToken ct = default)
    {
        ValidacaoDeProjeto.ValidarCliente(entrada.Nome, entrada.Observacoes);

        var cliente = new ProjetoCliente { Nome = entrada.Nome.Trim(), Observacoes = entrada.Observacoes, CriadoEm = DateTime.UtcNow };
        return await repositorio.CriarClienteAsync(cliente, ct);
    }

    public async Task AtualizarClienteAsync(int id, ClienteEntrada entrada, CancellationToken ct = default)
    {
        ValidacaoDeProjeto.ValidarCliente(entrada.Nome, entrada.Observacoes);

        var ok = await repositorio.AtualizarClienteAsync(id, entrada.Nome.Trim(), entrada.Observacoes, DateTime.UtcNow, ct);
        if (!ok) throw new KeyNotFoundException($"Cliente {id} não encontrado.");
    }

    public async Task ExcluirClienteAsync(int id, CancellationToken ct = default)
    {
        var cliente = await repositorio.ObterClienteComProjetosEPecasAsync(id, ct);
        if (cliente is null) return;

        var candidatos = cliente.Projetos.SelectMany(p => p.Pecas).Select(p => p.Arquivo).Distinct().ToList();

        await repositorio.ExcluirClienteAsync(id, ct);

        if (candidatos.Count > 0)
        {
            var emUso = await repositorio.ListarTodosArquivosDeProjetoAsync(ct);
            arquivos.LimparOrfaos(caminhos.PastaUploadsProjetos, emUso, candidatos);
        }
    }

    public async Task<ClienteComProjetos> ListarProjetosDoClienteAsync(int clienteId, CancellationToken ct = default)
    {
        var cliente = await repositorio.ObterClienteComProjetosEPecasAsync(clienteId, ct)
            ?? throw new KeyNotFoundException($"Cliente {clienteId} não encontrado.");

        var projetos = cliente.Projetos.Select(p =>
        {
            var pecasOrdenadas = p.Pecas.OrderBy(pp => pp.Ordem).ToList();
            var pecasPorUnidade = pecasOrdenadas.Sum(pp => pp.Quantidade);
            var capa = pecasOrdenadas.FirstOrDefault()?.Miniatura;

            return new ProjetoResumo(p.Id, p.Nome, p.Observacoes, p.CriadoEm, p.AtualizadoEm, pecasOrdenadas.Count, pecasPorUnidade, capa);
        }).ToList();

        return new ClienteComProjetos(cliente.Id, cliente.Nome, projetos);
    }

    public async Task<ProjetoDetalhado> ObterProjetoAsync(int id, CancellationToken ct = default)
    {
        var projeto = await repositorio.ObterProjetoAsync(id, ct)
            ?? throw new KeyNotFoundException($"Projeto {id} não encontrado.");

        return ParaDetalhado(projeto);
    }

    public async Task<int> CriarProjetoAsync(int clienteId, string nome, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome é obrigatório.", nameof(nome));

        var projeto = new Projeto { ClienteId = clienteId, Nome = nome.Trim(), CriadoEm = DateTime.UtcNow };
        return await repositorio.CriarProjetoAsync(projeto, ct);
    }

    public async Task AtualizarProjetoAsync(int id, ProjetoEntrada entrada, CancellationToken ct = default)
    {
        var pecas = entrada.Pecas
            .Select((p, indice) => ValidacaoDeProjeto.ArrumarPeca(p, indice))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();

        // Peças descartadas (arquivo antigo referenciado só por elas) viram órfãs — a mesma
        // faxina de exclusão de projeto cobre isso: candidatos = arquivos que existiam antes,
        // conferidos contra a tabela inteira depois da troca.
        var antes = await repositorio.ObterProjetoAsync(id, ct);
        var candidatosOrfaos = antes?.Pecas.Select(p => p.Arquivo).Distinct().ToList() ?? [];

        var ok = await repositorio.AtualizarProjetoAsync(
            id, entrada.Nome, entrada.Observacoes, entrada.LarguraTecido, entrada.Espaco, entrada.Margem, entrada.Giro,
            pecas, DateTime.UtcNow, ct);
        if (!ok) throw new KeyNotFoundException($"Projeto {id} não encontrado.");

        if (candidatosOrfaos.Count > 0)
        {
            var emUso = await repositorio.ListarTodosArquivosDeProjetoAsync(ct);
            arquivos.LimparOrfaos(caminhos.PastaUploadsProjetos, emUso, candidatosOrfaos);
        }
    }

    public async Task ExcluirProjetoAsync(int id, CancellationToken ct = default)
    {
        var projeto = await repositorio.ObterProjetoAsync(id, ct);
        if (projeto is null) return;

        var candidatos = projeto.Pecas.Select(p => p.Arquivo).Distinct().ToList();

        await repositorio.ExcluirProjetoAsync(id, ct);

        if (candidatos.Count > 0)
        {
            var emUso = await repositorio.ListarTodosArquivosDeProjetoAsync(ct);
            arquivos.LimparOrfaos(caminhos.PastaUploadsProjetos, emUso, candidatos);
        }
    }

    public async Task<int> PatchMiniaturasAsync(int projetoId, IReadOnlyList<MiniaturaEntrada> miniaturas, CancellationToken ct = default)
    {
        var idsValidos = await repositorio.ListarIdsDePecasAsync(projetoId, ct);

        var guardadas = 0;
        foreach (var m in miniaturas)
        {
            if (!idsValidos.Contains(m.PecaId)) continue;
            if (m.Miniatura is { Length: > ValidacaoDeProjeto.MiniaturaMaximoChars }) continue;

            await repositorio.PatchMiniaturaAsync(m.PecaId, m.Miniatura, ct);
            guardadas++;
        }

        return guardadas;
    }

    public async Task<string> SalvarImagemDeProjetoAsync(byte[] bytes, CancellationToken ct = default)
    {
        var extensao = arquivos.DetectarExtensao(bytes)
            ?? throw new ArgumentException("Formato de imagem não reconhecido.", nameof(bytes));

        var nomeArquivo = arquivos.GerarNomeSemColisao("projeto", extensao);
        await arquivos.SalvarAsync(caminhos.PastaUploadsProjetos, nomeArquivo, bytes, ct);
        return nomeArquivo;
    }

    private ProjetoDetalhado ParaDetalhado(Projeto projeto) => new(
        projeto.Id,
        projeto.ClienteId,
        projeto.Nome,
        projeto.Observacoes,
        projeto.LarguraTecido,
        projeto.Espaco,
        projeto.Margem,
        projeto.Giro,
        projeto.CriadoEm,
        projeto.AtualizadoEm,
        projeto.Pecas.OrderBy(p => p.Ordem).Select(p => new ProjetoPecaDto(
            p.Id, p.Nome, p.Arquivo, Path.Combine(caminhos.PastaUploadsProjetos, p.Arquivo), p.Largura, p.Altura, p.Quantidade, p.Ordem, p.Miniatura)).ToList());
}
