namespace OptimizePro.Painel;

public interface IConfiguracaoDeFaturamentoRepository
{
    /// <summary>Nunca retorna null — se ainda não foi configurada nesta instalação, devolve o padrão (tudo zerado, limite 7) sem gravar nada.</summary>
    Task<ConfiguracaoDeFaturamento> ObterAsync(CancellationToken ct = default);

    Task SalvarAsync(ConfiguracaoDeFaturamento configuracao, CancellationToken ct = default);
}
