namespace OptimizePro.Services.Configuracoes;

public interface IConfiguracaoService
{
    Task<ConfiguracoesDoApp> ObterAsync(CancellationToken ct = default);

    /// <exception cref="ArgumentException">DelayMaximoMs menor que DelayMinimoMs, ou campos obrigatórios vazios.</exception>
    Task SalvarAsync(ConfiguracoesDoApp configuracoes, CancellationToken ct = default);
}
