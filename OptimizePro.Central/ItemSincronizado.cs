namespace OptimizePro.Central;

/// <summary>Um item do lote que o app desktop envia (§24.2) — vira uma linha de <see cref="DadoSincronizado"/>.</summary>
public sealed record ItemSincronizado(string Tipo, string EntidadeId, string DadosJson, DateTime AtualizadoEm);
