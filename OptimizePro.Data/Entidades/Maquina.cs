using OptimizePro.Core.Impressoras;

namespace OptimizePro.Data.Entidades;

/// <summary>Porte de <c>imp_machines</c> (optmize-full, §22).</summary>
public class Maquina
{
    public required string Id { get; set; }
    public required string Nome { get; set; }
    public TipoDeMaquina Tipo { get; set; }
    public bool Habilitada { get; set; } = true;

    public string? Host { get; set; }
    public string? Ip { get; set; }

    public string? CaminhoHistorico { get; set; }
    public string? PastaPreview { get; set; }
    public string? PastaLogAoVivo { get; set; }
    public string? ArquivoLogAoVivo { get; set; }
    public string? PastaLogDeStatus { get; set; }
    public string? CaminhoListaDeTrabalhos { get; set; }
    public string? CaminhoEstatisticasDeTinta { get; set; }

    public string Origem { get; set; } = "manual";
    public int Posicao { get; set; }

    public DateTime? DescobertaEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    /// <summary>Assinatura barata (tamanho+data de modificação do arquivo de histórico) da última sincronização — evita reler o histórico inteiro quando nada mudou (§22.4).</summary>
    public string? UltimaAssinaturaHistorico { get; set; }

    public List<RegistroDeImpressao> Registros { get; set; } = [];
}
