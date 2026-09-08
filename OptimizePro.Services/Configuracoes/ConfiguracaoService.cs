using System.Globalization;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Services.Configuracoes;

public sealed class ConfiguracaoService(IConfiguracaoRepository repositorio) : IConfiguracaoService
{
    private const string ChaveCodigoPais = "codigo_pais_padrao";
    private const string ChaveDdd = "ddd_padrao";
    private const string ChaveMensagem = "mensagem_padrao_disparo";
    private const string ChaveDelayMinimo = "delay_minimo_ms";
    private const string ChaveDelayMaximo = "delay_maximo_ms";
    private const string ChaveDpi = "dpi_padrao_exportacao";

    public async Task<ConfiguracoesDoApp> ObterAsync(CancellationToken ct = default)
    {
        var valores = await repositorio.ObterTodasAsync(ct);
        var padrao = ConfiguracoesDoApp.Padrao;

        return new ConfiguracoesDoApp(
            CodigoPaisPadrao: ObterOuPadrao(valores, ChaveCodigoPais, padrao.CodigoPaisPadrao),
            DddPadrao: ObterOuPadrao(valores, ChaveDdd, padrao.DddPadrao),
            MensagemPadraoDeDisparo: ObterOuPadrao(valores, ChaveMensagem, padrao.MensagemPadraoDeDisparo),
            DelayMinimoMs: ObterIntOuPadrao(valores, ChaveDelayMinimo, padrao.DelayMinimoMs),
            DelayMaximoMs: ObterIntOuPadrao(valores, ChaveDelayMaximo, padrao.DelayMaximoMs),
            DpiPadraoDeExportacao: ObterIntOuPadrao(valores, ChaveDpi, padrao.DpiPadraoDeExportacao));
    }

    public async Task SalvarAsync(ConfiguracoesDoApp configuracoes, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(configuracoes.CodigoPaisPadrao))
            throw new ArgumentException("Código do país é obrigatório.", nameof(configuracoes));

        if (string.IsNullOrWhiteSpace(configuracoes.DddPadrao))
            throw new ArgumentException("DDD padrão é obrigatório.", nameof(configuracoes));

        if (configuracoes.DelayMaximoMs < configuracoes.DelayMinimoMs)
            throw new ArgumentException("O delay máximo não pode ser menor que o mínimo.", nameof(configuracoes));

        if (configuracoes.DpiPadraoDeExportacao <= 0)
            throw new ArgumentException("DPI de exportação precisa ser maior que zero.", nameof(configuracoes));

        await repositorio.DefinirAsync(ChaveCodigoPais, configuracoes.CodigoPaisPadrao, ct);
        await repositorio.DefinirAsync(ChaveDdd, configuracoes.DddPadrao, ct);
        await repositorio.DefinirAsync(ChaveMensagem, configuracoes.MensagemPadraoDeDisparo, ct);
        await repositorio.DefinirAsync(ChaveDelayMinimo, configuracoes.DelayMinimoMs.ToString(CultureInfo.InvariantCulture), ct);
        await repositorio.DefinirAsync(ChaveDelayMaximo, configuracoes.DelayMaximoMs.ToString(CultureInfo.InvariantCulture), ct);
        await repositorio.DefinirAsync(ChaveDpi, configuracoes.DpiPadraoDeExportacao.ToString(CultureInfo.InvariantCulture), ct);
    }

    private static string ObterOuPadrao(Dictionary<string, string?> valores, string chave, string padrao) =>
        valores.TryGetValue(chave, out var v) && !string.IsNullOrEmpty(v) ? v : padrao;

    private static int ObterIntOuPadrao(Dictionary<string, string?> valores, string chave, int padrao) =>
        valores.TryGetValue(chave, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : padrao;
}
