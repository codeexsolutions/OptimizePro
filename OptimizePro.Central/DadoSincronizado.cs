namespace OptimizePro.Central;

/// <summary>
/// Uma linha sincronizada de uma instalação (§24.2) — em vez de espelhar cada tabela
/// operacional (Maquina/RegistroDeImpressao/Pedido/OrdemDeServico/Usuario/
/// ConfiguracaoDeFaturamento) com seu próprio schema+migração na Central, guarda o dado como
/// JSON (Postgres tem JSONB nativo, com índice e consulta decente) — a Central não é o
/// sistema de registro de nada disso, é só um cache de leitura pro painel remoto. Schema
/// mudando do lado local (§22/§23) não vira migração aqui; quem lê decide o formato.
/// </summary>
public class DadoSincronizado
{
    public required string InstalacaoId { get; set; }

    /// <summary>"maquina" | "registro_impressao" | "pedido" | "ordem_servico" | "usuario" | "faturamento" — ver <see cref="TipoDeDadoSincronizado"/>.</summary>
    public required string Tipo { get; set; }

    public required string EntidadeId { get; set; }

    public required string DadosJson { get; set; }

    public DateTime AtualizadoEm { get; set; }

    public Instalacao? Instalacao { get; set; }
}

/// <summary>Os tipos de dado que a fase 4 sincroniza — string em vez de enum porque é o valor gravado na coluna <see cref="DadoSincronizado.Tipo"/>, e trocar seus nomes viraria migração de dado, não só de schema.</summary>
public static class TipoDeDadoSincronizado
{
    public const string Maquina = "maquina";
    public const string RegistroDeImpressao = "registro_impressao";
    public const string Pedido = "pedido";
    public const string OrdemDeServico = "ordem_servico";
    public const string Usuario = "usuario";
    public const string Faturamento = "faturamento";
}
