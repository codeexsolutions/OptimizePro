using OptimizePro.Core.Impressoras;

namespace OptimizePro.Services.Impressoras;

/// <summary>Uma linha do resultado de uma varredura já concluída — porte do item de <c>scan.results</c> em <c>routes/machines.js</c>.</summary>
public sealed record ResultadoDaVarredura(
    string Acao, // "pendente" | "atualizada" | "cadastrada"
    string Host,
    string? Ip,
    TipoDeMaquina Tipo,
    string RotuloDoTipo,
    string Share,
    string Raiz,
    string? MaquinaId,
    string? MaquinaNome);

/// <summary>Estado (compartilhado, singleton) da varredura em andamento — só uma por vez, igual à referência.</summary>
public sealed class EstadoDaVarredura
{
    public bool EmExecucao { get; set; }
    public string Fase { get; set; } = "idle";
    public int Escaneados { get; set; }
    public int Total { get; set; }
    public string Mensagem { get; set; } = "Nenhuma varredura executada ainda";
    public int Alcancaveis { get; set; }
    public List<ResultadoDaVarredura> Resultados { get; set; } = [];
    public string? Erro { get; set; }

    public EstadoDaVarredura Clonar() => new()
    {
        EmExecucao = EmExecucao,
        Fase = Fase,
        Escaneados = Escaneados,
        Total = Total,
        Mensagem = Mensagem,
        Alcancaveis = Alcancaveis,
        Resultados = [.. Resultados],
        Erro = Erro,
    };
}
