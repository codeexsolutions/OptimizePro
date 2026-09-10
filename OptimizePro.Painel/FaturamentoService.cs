namespace OptimizePro.Painel;

public sealed class FaturamentoService(IConfiguracaoDeFaturamentoRepository configuracaoRepositorio, IUsuarioRepository usuarioRepositorio) : IFaturamentoService
{
    public Task<ConfiguracaoDeFaturamento> ObterConfiguracaoAsync(CancellationToken ct = default) =>
        configuracaoRepositorio.ObterAsync(ct);

    public Task AtualizarConfiguracaoAsync(decimal valorBaseMensal, decimal valorPorUsuarioExtra, int limiteDeUsuariosNoPlano, CancellationToken ct = default)
    {
        if (valorBaseMensal < 0) throw new ArgumentOutOfRangeException(nameof(valorBaseMensal), "O valor base não pode ser negativo.");
        if (valorPorUsuarioExtra < 0) throw new ArgumentOutOfRangeException(nameof(valorPorUsuarioExtra), "O valor por usuário extra não pode ser negativo.");
        if (limiteDeUsuariosNoPlano < 1) throw new ArgumentOutOfRangeException(nameof(limiteDeUsuariosNoPlano), "O limite de usuários do plano precisa ser pelo menos 1.");

        return configuracaoRepositorio.SalvarAsync(new ConfiguracaoDeFaturamento
        {
            ValorBaseMensal = valorBaseMensal,
            ValorPorUsuarioExtra = valorPorUsuarioExtra,
            LimiteDeUsuariosNoPlano = limiteDeUsuariosNoPlano,
        }, ct);
    }

    public async Task<ResumoDeFaturamento> CalcularMensalidadeAsync(CancellationToken ct = default)
    {
        var configuracao = await configuracaoRepositorio.ObterAsync(ct);
        var usuariosHabilitados = await usuarioRepositorio.ContarHabilitadosAsync(ct);
        var usuariosExtras = Math.Max(0, usuariosHabilitados - configuracao.LimiteDeUsuariosNoPlano);
        var mensalidadeTotal = configuracao.ValorBaseMensal + usuariosExtras * configuracao.ValorPorUsuarioExtra;

        return new ResumoDeFaturamento(
            configuracao.ValorBaseMensal,
            configuracao.ValorPorUsuarioExtra,
            configuracao.LimiteDeUsuariosNoPlano,
            usuariosHabilitados,
            usuariosExtras,
            mensalidadeTotal);
    }
}
