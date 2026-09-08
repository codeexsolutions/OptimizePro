namespace OptimizePro.Services.Configuracoes;

/// <summary>Preferências do usuário (§7.2/§15 — defaults de disparo e exportação), persistidas na tabela chave/valor <c>configuracao_app</c>.</summary>
public sealed record ConfiguracoesDoApp(
    string CodigoPaisPadrao,
    string DddPadrao,
    string MensagemPadraoDeDisparo,
    int DelayMinimoMs,
    int DelayMaximoMs,
    int DpiPadraoDeExportacao)
{
    /// <summary>Defaults de <c>config.js</c> (§7.2) — DPI de exportação não tinha default na spec original (item novo do desktop); 300 é o padrão de impressão comum, escolha própria.</summary>
    public static ConfiguracoesDoApp Padrao => new(
        CodigoPaisPadrao: "55",
        DddPadrao: "11",
        MensagemPadraoDeDisparo: "Olá {{nome}}, tudo bem? Essa é uma mensagem automática de teste do disparo.",
        DelayMinimoMs: 8000,
        DelayMaximoMs: 20000,
        DpiPadraoDeExportacao: 300);
}
