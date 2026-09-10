namespace OptimizePro.Sincronizacao;

/// <summary>Mesma forma de <c>OptimizePro.Central.ItemSincronizado</c> — duplicado de propósito (os dois lados do isolamento §23/§24 não se referenciam); é só um contrato HTTP, não um tipo compartilhado.</summary>
public sealed record ItemParaSincronizar(string Tipo, string EntidadeId, string DadosJson, DateTime AtualizadoEm);

/// <summary>Os mesmos valores de <c>OptimizePro.Central.TipoDeDadoSincronizado</c>.</summary>
public static class TipoDeItem
{
    public const string Maquina = "maquina";
    public const string RegistroDeImpressao = "registro_impressao";
    public const string Pedido = "pedido";
    public const string OrdemDeServico = "ordem_servico";
    public const string Usuario = "usuario";
    public const string Faturamento = "faturamento";
}
