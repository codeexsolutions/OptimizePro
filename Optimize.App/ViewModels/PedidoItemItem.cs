using System.Linq;
using OptimizePro.Data.Entidades;
using OptimizePro.Services.Impressoras.Historico;

namespace Optimize.App.ViewModels;

/// <summary>Um item dentro do detalhe de um pedido (§22.8) — já formatado pra tela.</summary>
public sealed class PedidoItemItem(PedidoItem item)
{
    public string Id { get; } = item.Id;
    public int Posicao { get; } = item.Posicao;
    public string? Tarefa { get; } = item.Tarefa;
    public string ClienteETecido { get; } = string.Join(" · ", new[] { item.NomeDoCliente, item.Tecido }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public string? NomeDaMaquina { get; } = item.NomeDaMaquina;
    public string DataFormatada { get; } = FormatoImpressoras.DataBr(item.Data);
    public string Metragem { get; } = FormatoImpressoras.Metros(item.ComprimentoDeImpressao ?? 0);
    public string StatusNaCalandra { get; } = item.StatusNaCalandra;
    public string? Motivo { get; } = item.MotivoPersonalizadoNaCalandra ?? item.MotivoNaCalandra;
}
