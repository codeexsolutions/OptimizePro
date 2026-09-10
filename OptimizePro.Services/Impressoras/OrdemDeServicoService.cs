using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Services.Impressoras;

public sealed class OrdemDeServicoService(IOrdemDeServicoRepository repositorio) : IOrdemDeServicoService
{
    public Task<string> CriarAsync(
        string nomeDoCliente, string? tecido, string? tamanhoDeImpressao, double? metros,
        string? operador, string? maquina, string data, string? observacao,
        IReadOnlyList<ImagemParaOrdemDeServico> imagens, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nomeDoCliente))
            throw new ArgumentException("Informe o nome do cliente.", nameof(nomeDoCliente));
        if (string.IsNullOrWhiteSpace(data))
            throw new ArgumentException("Informe a data.", nameof(data));

        var ordem = new OrdemDeServico
        {
            Id = "",
            NomeDoCliente = nomeDoCliente.Trim(),
            Tecido = tecido,
            TamanhoDeImpressao = tamanhoDeImpressao,
            Metros = metros,
            Operador = operador,
            Maquina = maquina,
            Data = data,
            Observacao = observacao,
            Imagens = imagens.Select(img => new OrdemDeServicoImagem
            {
                Id = "",
                OrdemId = "",
                NomeDoArquivo = img.NomeDoArquivo,
                TipoMime = img.TipoMime,
                Dados = img.Dados,
            }).ToList(),
        };

        return repositorio.CriarAsync(ordem, ct);
    }

    public Task<List<ResumoDeOrdemDeServico>> ListarAsync(string? busca = null, CancellationToken ct = default) =>
        repositorio.ListarAsync(busca, ct);

    public Task<OrdemDeServico?> ObterAsync(string id, CancellationToken ct = default) => repositorio.ObterAsync(id, ct);

    public Task<bool> ExcluirAsync(string id, CancellationToken ct = default) => repositorio.ExcluirAsync(id, ct);
}
