namespace OptimizePro.Core.Encaixe.Busca;

/// <summary>Histórico de uma receita dentro de uma busca (§11.7) — melhor consumo já visto, quantas vezes foi tentada, e quantas vezes rendeu um novo recorde global da busca (§12.1 — vira exemplo de treino da rede das receitas).</summary>
public sealed class PlacarDeReceita(Receita receita)
{
    public Receita Receita { get; } = receita;
    public double? MelhorConsumoCm { get; private set; }

    /// <summary>Quantos itens ficaram de fora na melhor tentativa desta receita (§11.7, porte de melhoria do projeto de referência — <c>int.MaxValue</c> até a 1ª tentativa registrar algo).</summary>
    public int MelhorNaoEncaixados { get; private set; } = int.MaxValue;

    public IReadOnlyList<int>? MelhorOrdem { get; private set; }
    public int Tentativas { get; private set; }
    public int Vitorias { get; private set; }

    /// <summary>Índices (em <see cref="MelhorOrdem"/>) da unidade que sobrou mais buraco morto na melhor tentativa desta receita (§11.7, "reparo guiado", porte de melhoria do projeto de referência) — alimenta <see cref="Embaralhamento.RepararPior"/>.</summary>
    public IReadOnlyList<int>? MelhorPiorUnidadeItens { get; private set; }

    public void Registrar(double consumoCm, int naoEncaixados, IReadOnlyList<int> ordem, IReadOnlyList<int>? piorUnidadeItens = null)
    {
        Tentativas++;
        if (MelhorConsumoCm is null || EhMelhor(naoEncaixados, consumoCm, MelhorNaoEncaixados, MelhorConsumoCm.Value))
        {
            MelhorConsumoCm = consumoCm;
            MelhorNaoEncaixados = naoEncaixados;
            MelhorOrdem = ordem;
            MelhorPiorUnidadeItens = piorUnidadeItens;
        }
    }

    /// <summary>Chamado quando uma tentativa desta receita vira o novo melhor resultado global da busca (não só o melhor da própria receita).</summary>
    public void RegistrarVitoriaGlobal() => Vitorias++;

    /// <summary>
    /// Porte de <c>melhorQue</c> do projeto de referência (§11.7) — bug real achado nele e que
    /// nosso port também tinha: comparar só por <c>ConsumoCm</c> deixa uma tentativa que deixou
    /// PEÇA DE FORA parecer "melhor" só porque gastou menos tecido (menos peça, menos tecido, número
    /// mais baixo — mas o encaixe está incompleto). Menos itens não-encaixados sempre vence,
    /// não importa o consumo; consumo só desempata quando os dois têm a mesma quantidade de
    /// itens de fora (o caso comum: ambos com 0).
    /// </summary>
    public static bool EhMelhor(int naoEncaixadosCandidato, double consumoCandidato, int naoEncaixadosAtual, double consumoAtual) =>
        naoEncaixadosCandidato != naoEncaixadosAtual
            ? naoEncaixadosCandidato < naoEncaixadosAtual
            : consumoCandidato < consumoAtual;
}
