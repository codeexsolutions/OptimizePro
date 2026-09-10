using OptimizePro.Core.Impressoras;

namespace OptimizePro.Services.Impressoras;

/// <summary>Uma máquina que a varredura de rede achou e identificou (porte de <c>discovery.js</c>, §22).</summary>
public sealed class MaquinaEncontrada
{
    public required string Host { get; init; }
    public string? Ip { get; init; }
    public required TipoDeMaquina Tipo { get; init; }
    public required string Share { get; init; }
    public required string Raiz { get; init; }

    public string? CaminhoHistorico { get; init; }
    public string? PastaPreview { get; init; }
    public string? PastaLogAoVivo { get; init; }
    public string? ArquivoLogAoVivo { get; init; }
    public string? PastaLogDeStatus { get; init; }
    public string? CaminhoListaDeTrabalhos { get; init; }
    public string? CaminhoEstatisticasDeTinta { get; init; }
}

/// <summary>Progresso da varredura, pra tela mostrar uma barra/mensagem em vez de um botão travado por meio minuto.</summary>
public sealed record ProgressoDaVarredura(string Fase, int Escaneados, int Total, string? Mensagem = null);
