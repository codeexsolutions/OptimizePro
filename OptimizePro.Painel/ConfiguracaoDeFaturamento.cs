namespace OptimizePro.Painel;

/// <summary>
/// Os valores do plano desta instalação (§23.2) — linha única (Id sempre 1), configurada por
/// quem vende/administra o Optimize, não pelo proprietário da fábrica. "Vence em" NÃO mora
/// aqui: isso já existe em <c>LicencaService.ObterEstado().ValidoAte</c> (outro projeto, de
/// propósito — ver nota de isolamento em <c>OptimizePro.Painel.csproj</c>); quem compõe os
/// dois pra tela de Faturamento é a camada de API, na fase 3.
/// </summary>
public class ConfiguracaoDeFaturamento
{
    public int Id { get; set; } = 1;

    public decimal ValorBaseMensal { get; set; }
    public decimal ValorPorUsuarioExtra { get; set; }

    /// <summary>Quantos usuários habilitados o valor base já cobre — padrão 7 (§23.2, decisão do usuário 10/09/2026).</summary>
    public int LimiteDeUsuariosNoPlano { get; set; } = 7;

    public DateTime? AtualizadoEm { get; set; }
}
