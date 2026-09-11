namespace OptimizePro.Central;

/// <summary>
/// Mesma forma de <c>OptimizePro.Sincronizacao.FaturamentoDto</c> — o que chega dentro de
/// <see cref="DadoSincronizado.DadosJson"/> quando <c>Tipo == TipoDeDadoSincronizado.Faturamento</c>.
/// <c>LicencaValidaAte</c> é a mesma data de <c>LicencaService.ObterEstado().ValidoAte</c> do
/// app desktop — viaja junto porque a tela de Faturamento do painel remoto (§24.8) mostra as
/// duas coisas juntas ("mensalidade" + "vence em").
/// </summary>
public sealed record FaturamentoSincronizadoDto(
    decimal ValorBaseMensal, decimal ValorPorUsuarioExtra, int LimiteDeUsuariosNoPlano, DateOnly? LicencaValidaAte);
