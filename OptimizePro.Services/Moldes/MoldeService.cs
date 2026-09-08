using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;
using OptimizePro.Services.Arquivos;
using OptimizePro.Services.Armazenamento;

namespace OptimizePro.Services.Moldes;

public sealed class MoldeService(IMoldeRepository repositorio, IArquivoService arquivos, CaminhosDoApp caminhos) : IMoldeService
{
    private static readonly Dictionary<string, string> ExtensaoPorContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = "png",
        ["image/jpeg"] = "jpg",
        ["image/webp"] = "webp",
        ["image/gif"] = "gif",
    };

    public IReadOnlyList<string> ListarPapeis() => Papeis.Lista;

    public async Task<IReadOnlyList<MoldeResumo>> ListarAsync(CancellationToken ct = default)
    {
        var moldes = await repositorio.ListarAsync(ct);
        var pecas = await repositorio.ListarResumoDePecasAsync(ct);
        var porMolde = pecas.ToLookup(p => p.MoldeId);

        return moldes.Select(m =>
        {
            var doMolde = porMolde[m.Id].ToList();
            var tamanhos = doMolde.Select(p => p.Tamanho).Distinct().ToList();
            var totalPecas = doMolde.Sum(p => p.Quantidade);

            // Cada tamanho novo herda a mesma estrutura de partes do primeiro (§8.3), então o
            // primeiro tamanho representa as peças de uma única unidade.
            var primeiroTamanho = tamanhos.FirstOrDefault();
            var pecasPorUnidade = primeiroTamanho is null
                ? 0
                : doMolde.Where(p => p.Tamanho == primeiroTamanho).Sum(p => p.Quantidade);

            return new MoldeResumo(m.Id, m.Nome, m.Observacoes, m.CriadoEm, m.AtualizadoEm, tamanhos, totalPecas, pecasPorUnidade);
        }).ToList();
    }

    public async Task<MoldeDetalhado> ObterAsync(int id, CancellationToken ct = default)
    {
        var molde = await repositorio.ObterAsync(id, ct)
            ?? throw new KeyNotFoundException($"Molde {id} não encontrado.");

        return ParaDetalhado(molde);
    }

    public async Task<int> CriarAsync(MoldeEntrada entrada, CancellationToken ct = default)
    {
        var pecas = ArrumarPecas(entrada.Pecas);

        var molde = new Molde
        {
            Nome = entrada.Nome,
            Observacoes = entrada.Observacoes,
            CriadoEm = DateTime.UtcNow,
            Pecas = pecas,
        };

        return await repositorio.CriarAsync(molde, ct);
    }

    public async Task AtualizarAsync(int id, MoldeEntrada entrada, CancellationToken ct = default)
    {
        var pecas = ArrumarPecas(entrada.Pecas);

        var ok = await repositorio.AtualizarAsync(id, entrada.Nome, entrada.Observacoes, pecas, DateTime.UtcNow, ct);
        if (!ok) throw new KeyNotFoundException($"Molde {id} não encontrado.");
    }

    public async Task ExcluirAsync(int id, CancellationToken ct = default)
    {
        var molde = await repositorio.ObterAsync(id, ct);
        if (molde is null) return;

        var candidatos = molde.Artes.SelectMany(a => a.Pecas).Select(p => p.Arquivo).Distinct().ToList();

        await repositorio.ExcluirAsync(id, ct);

        if (candidatos.Count > 0)
        {
            var emUso = await repositorio.ListarTodosArquivosDeArteAsync(ct);
            arquivos.LimparOrfaos(caminhos.PastaUploadsArtesMolde, emUso, candidatos);
        }
    }

    public async Task<string> SalvarImagemDeArteAsync(int moldeId, string papel, byte[] bytes, string? contentType, CancellationToken ct = default)
    {
        var extensao = arquivos.DetectarExtensao(bytes)
            ?? (contentType is not null && ExtensaoPorContentType.TryGetValue(contentType, out var ext) ? ext : null)
            ?? throw new ArgumentException("Formato de imagem não reconhecido.", nameof(bytes));

        var nomeArquivo = arquivos.GerarNomeSemColisao("arte", extensao);
        await arquivos.SalvarAsync(caminhos.PastaUploadsArtesMolde, nomeArquivo, bytes, ct);
        return nomeArquivo;
    }

    public async Task<IReadOnlyList<EstampaDto>> ListarEstampasAsync(int moldeId, CancellationToken ct = default)
    {
        var artes = await repositorio.ListarArtesAsync(moldeId, ct);
        return artes.Select(ParaEstampaDto).ToList();
    }

    public async Task<int> SalvarEstampaAsync(int moldeId, EstampaEntrada entrada, CancellationToken ct = default)
    {
        if (!await repositorio.ExisteAsync(moldeId, ct))
            throw new KeyNotFoundException($"Molde {moldeId} não encontrado.");

        var candidatosOrfaos = new List<string>();
        if (entrada.Id is { } arteId)
        {
            var artes = await repositorio.ListarArtesAsync(moldeId, ct);
            var existente = artes.FirstOrDefault(a => a.Id == arteId);
            if (existente is not null)
                candidatosOrfaos = existente.Pecas.Select(p => p.Arquivo).Distinct().ToList();
        }

        var pecas = entrada.Pecas.Select(p => new MoldeArtePeca
        {
            Papel = p.Papel,
            Arquivo = p.Arquivo,
            NomeOriginal = p.NomeOriginal,
            Ajuste = ValidacaoDeMolde.ArrumarAjuste(p.Ajuste),
        }).ToList();

        var id = await repositorio.UpsertArteAsync(moldeId, entrada.Id, entrada.Nome, pecas, DateTime.UtcNow, ct)
            ?? throw new KeyNotFoundException($"Estampa {entrada.Id} não encontrada no molde {moldeId}.");

        if (candidatosOrfaos.Count > 0)
        {
            var emUso = await repositorio.ListarTodosArquivosDeArteAsync(ct);
            arquivos.LimparOrfaos(caminhos.PastaUploadsArtesMolde, emUso, candidatosOrfaos);
        }

        return id;
    }

    public async Task ExcluirEstampaAsync(int moldeId, int arteId, CancellationToken ct = default)
    {
        var artes = await repositorio.ListarArtesAsync(moldeId, ct);
        var existente = artes.FirstOrDefault(a => a.Id == arteId);
        var candidatos = existente?.Pecas.Select(p => p.Arquivo).Distinct().ToList() ?? [];

        await repositorio.ExcluirArteAsync(moldeId, arteId, ct);

        if (candidatos.Count > 0)
        {
            var emUso = await repositorio.ListarTodosArquivosDeArteAsync(ct);
            arquivos.LimparOrfaos(caminhos.PastaUploadsArtesMolde, emUso, candidatos);
        }
    }

    private static List<MoldePeca> ArrumarPecas(IReadOnlyList<PecaEntrada> entradas)
    {
        var pecas = entradas
            .Select((entrada, indice) => ValidacaoDeMolde.ArrumarPeca(entrada, indice))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();

        if (pecas.Count == 0)
            throw new ArgumentException("Nenhuma peça válida informada.", nameof(entradas));

        return pecas;
    }

    private MoldeDetalhado ParaDetalhado(Molde molde) => new(
        molde.Id,
        molde.Nome,
        molde.Observacoes,
        molde.CriadoEm,
        molde.AtualizadoEm,
        molde.Pecas.OrderBy(p => p.Ordem).Select(p => new PecaDto(
            p.Id, p.Tamanho, p.Papel, p.Nome, p.Quantidade, p.Largura, p.Altura, p.Contorno, p.Furos, p.Origem, p.Ordem)).ToList(),
        molde.Artes.Select(ParaEstampaDto).ToList());

    private EstampaDto ParaEstampaDto(MoldeArte arte) => new(
        arte.Id,
        arte.Nome,
        arte.Pecas.Select(p => new EstampaPecaDto(
            p.Papel, p.Arquivo, Path.Combine(caminhos.PastaUploadsArtesMolde, p.Arquivo), p.NomeOriginal, p.Ajuste)).ToList());
}
