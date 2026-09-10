using System.Linq;
using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Services.Impressoras;

public sealed class PedidoService(IPedidoRepository repositorio) : IPedidoService
{
    public Task<string> CriarAsync(IReadOnlyList<ItemParaPedido> itens, string? observacao, CancellationToken ct = default)
    {
        var pedidoItens = itens.Select(item =>
        {
            var (cliente, tecido) = TextoDeImpressoras.SepararClienteETecido(item.Tarefa);
            return new PedidoItem
            {
                Id = "", // preenchido pelo repositório
                PedidoId = "",
                RegistroId = item.RegistroId,
                NomeDoCliente = cliente,
                Tecido = tecido,
                Tarefa = item.Tarefa,
                MaquinaId = item.MaquinaId,
                NomeDaMaquina = item.NomeDaMaquina,
                ComprimentoDeImpressao = item.ComprimentoDeImpressao,
                Data = item.Data,
            };
        }).ToList();

        return repositorio.CriarAsync(pedidoItens, observacao, ct);
    }

    public Task<List<ResumoDoPedido>> ListarAsync(CancellationToken ct = default) => repositorio.ListarAsync(ct);

    public Task<Pedido?> ObterAsync(string id, CancellationToken ct = default) => repositorio.ObterAsync(id, ct);

    public Task<bool> ExcluirAsync(string id, CancellationToken ct = default) => repositorio.ExcluirAsync(id, ct);

    public Task<bool> AtualizarStatusAsync(string id, string status, CancellationToken ct = default) => repositorio.AtualizarStatusAsync(id, status, ct);

    public Task<PedidoItem?> AtualizarResultadoDoItemAsync(string pedidoId, string itemId, string status, string? motivo, string? motivoPersonalizado, CancellationToken ct = default) =>
        repositorio.AtualizarResultadoDoItemAsync(pedidoId, itemId, status, motivo, motivoPersonalizado, ct);
}
