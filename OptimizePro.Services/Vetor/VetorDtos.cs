namespace OptimizePro.Services.Vetor;

/// <summary>Parâmetros de vetorização (§14.1, tabela de parâmetros) — todos vindos da UI.</summary>
public sealed record OpcoesDeVetorizacao(
    int Cores = 6,
    int Detalhe = 8,
    double Suavidade = 1,
    double QuinaGraus = 55,
    double Tensao = 1,
    bool Redondas = true,
    bool Subpixel = true,
    double JuntarSombras = 0,
    // "Plano B" (02/09/2026) — em vez do traçado próprio (contorno + Douglas-Peucker +
    // ajuste de reta/arco), usa o Potrace de verdade via processo externo
    // (PotraceProcessoService), pra arte complexa onde o traçado próprio ainda distorce
    // forma. Precisa do executável VetorGpl presente (não fica embutido no app principal
    // por causa da licença GPL — ver README em Ferramentas/VetorGpl); se não encontrar,
    // VetorService lança com uma mensagem clara em vez de tentar continuar com o motor
    // próprio silenciosamente (trocar de motor sem avisar seria surpreendente).
    bool UsarMotorExterno = false);

public sealed record ResultadoDeVetorizacao(string Svg, int LarguraPx, int AlturaPx);

/// <summary>
/// 4 atalhos pré-configurados (§14.1). A especificação cita os nomes mas não os valores
/// exatos de cada um — escolha própria, documentada aqui por ser interpretação e não
/// extração literal do texto original.
/// </summary>
public static class AtalhosDeVetorizacao
{
    // Cores subiu de 4 pra 6 (02/09/2026) — medido em logo real com degradê + texto: com só 4
    // baldes, o quantizador é obrigado a misturar cores de regiões sem relação (ex.: um tom de
    // degradê com o branco do texto), saindo como vazamento de cor entre formas. 6 já separa
    // bem os casos comuns; pra imagens ainda mais ricas em cor, ver SugerirNumeroDeCores.
    public static readonly OpcoesDeVetorizacao Chapada = new(Cores: 6, Detalhe: 12, Suavidade: 2, QuinaGraus: 55, Tensao: 1, Redondas: true, Subpixel: false, JuntarSombras: 0);
    public static readonly OpcoesDeVetorizacao Silhueta = new(Cores: 1, Detalhe: 8, Suavidade: 1, QuinaGraus: 55, Tensao: 1, Redondas: true, Subpixel: true, JuntarSombras: 0);
    public static readonly OpcoesDeVetorizacao Sombra = new(Cores: 6, Detalhe: 8, Suavidade: 1, QuinaGraus: 55, Tensao: 1, Redondas: true, Subpixel: true, JuntarSombras: 60);
    public static readonly OpcoesDeVetorizacao Fino = new(Cores: 10, Detalhe: 2, Suavidade: 0.5, QuinaGraus: 40, Tensao: 1, Redondas: true, Subpixel: true, JuntarSombras: 0);
}
