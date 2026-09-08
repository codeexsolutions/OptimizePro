namespace OptimizePro.Data.Entidades;

/// <summary>Tabela <c>encaixe_receitas</c> (§3.5, §11.12/§11.7) — placar por (assinatura, receita).</summary>
public class EncaixeReceita
{
    public int Id { get; set; }

    /// <summary>Agrupa trabalhos "parecidos" (§11.12: forma/proporção das peças, não nome/quantidade).</summary>
    public required string Assinatura { get; set; }

    /// <summary>Motor+agrupamento+ordem+heurística, serializado (a mesma forma que <c>Receita</c> do Core representa).</summary>
    public required string Receita { get; set; }

    public int Usos { get; set; }
    public int Vitorias { get; set; }
    public DateTime AtualizadoEm { get; set; }
}

/// <summary>Tabela <c>encaixe_guardados</c> (§3.6) — melhor encaixe já visto pra um trabalho exato.</summary>
public class EncaixeGuardado
{
    /// <summary>Identifica o trabalho EXATO (chave primária) — diferente de <see cref="Assinatura"/>, que agrupa trabalhos parecidos.</summary>
    public required string Chave { get; set; }

    public string? Assinatura { get; set; }
    public double? LarguraTecido { get; set; }
    public double? Espaco { get; set; }
    public double? Margem { get; set; }

    /// <summary>Metragem consumida — quanto menor, melhor.</summary>
    public double Consumo { get; set; }

    public double? Aproveitamento { get; set; }

    /// <summary>JSON opcional — formato interno não fixado ainda (nenhum tipo de Core cobre "peças" genérico o bastante); fica cru até a camada de Services definir o DTO.</summary>
    public string? PecasJson { get; set; }

    /// <summary>JSON — posição de cada peça no resultado salvo; mesma observação de <see cref="PecasJson"/>.</summary>
    public required string PosicoesJson { get; set; }

    public string? Receita { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}

/// <summary>Tabela <c>encaixe_historico</c> (§3.10) — log de tentativas, só pra análise/depuração, e matéria-prima do treino da rede das receitas (§12.1).</summary>
public class EncaixeHistorico
{
    public int Id { get; set; }
    public required string Assinatura { get; set; }
    public double? LarguraTecido { get; set; }
    public int? Pecas { get; set; }
    public double? Consumo { get; set; }
    public double? Aproveitamento { get; set; }
    public string? Receita { get; set; }
    public int? Tentativas { get; set; }
    public DateTime CriadoEm { get; set; }

    /// <summary>JSON do vetor de 12 números do trabalho (§12.1, <c>VetorizacaoDoTrabalho</c>) — nulo em linhas de antes da rede das receitas existir.</summary>
    public string? FeaturesJson { get; set; }

    /// <summary>JSON do placar de todas as receitas tentadas nesta busca (§12.1) — nulo pelo mesmo motivo.</summary>
    public string? PlacarJson { get; set; }
}

/// <summary>Tabela <c>encaixe_rede_pesos</c> (§3.10.1, §12.1) — os pesos da rede das receitas; uma linha só (id=1), reescrita a cada retreino.</summary>
public class EncaixeRedePesos
{
    public int Id { get; set; } = 1;

    /// <summary>JSON de <c>OptimizePro.Core.Encaixe.Busca.RedeNeural</c> (tamanhos + camadas com pesos/vieses).</summary>
    public required string PesosJson { get; set; }

    /// <summary>Quantos exemplos de treino foram usados no último retreino — decide quando vale treinar de novo e quando a rede já viu trabalho suficiente pra busca confiar nela (§12.1).</summary>
    public int Exemplos { get; set; }

    public DateTime AtualizadoEm { get; set; }
}
