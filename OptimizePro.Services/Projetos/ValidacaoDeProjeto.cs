using OptimizePro.Data.Entidades;

namespace OptimizePro.Services.Projetos;

/// <summary>Porte das validações de <c>projetos-api.js</c> (§4.3 da especificação).</summary>
internal static class ValidacaoDeProjeto
{
    public const int NomeClienteMaximo = 120;
    public const int ObservacoesClienteMaximo = 500;
    public const int MiniaturaMaximoChars = 200_000;

    public static void ValidarCliente(string nome, string? observacoes)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome é obrigatório.", nameof(nome));

        if (nome.Length > NomeClienteMaximo)
            throw new ArgumentException($"Nome deve ter no máximo {NomeClienteMaximo} caracteres.", nameof(nome));

        if (observacoes is { Length: > ObservacoesClienteMaximo })
            throw new ArgumentException($"Observações devem ter no máximo {ObservacoesClienteMaximo} caracteres.", nameof(observacoes));
    }

    /// <summary>Retorna a peça pronta pra persistir, ou null se inválida (sem arquivo, largura/altura ≤0, ou miniatura acima do limite).</summary>
    public static ProjetoPeca? ArrumarPeca(ProjetoPecaEntrada entrada, int ordem)
    {
        if (string.IsNullOrWhiteSpace(entrada.Arquivo)) return null;
        if (entrada.Largura <= 0 || entrada.Altura <= 0) return null;
        if (entrada.Miniatura is { Length: > MiniaturaMaximoChars }) return null;

        return new ProjetoPeca
        {
            Nome = string.IsNullOrWhiteSpace(entrada.Nome) ? "Peça" : entrada.Nome.Trim(),
            Arquivo = entrada.Arquivo.Trim(),
            Largura = entrada.Largura,
            Altura = entrada.Altura,
            Quantidade = Math.Max(1, entrada.Quantidade),
            Ordem = ordem,
            Miniatura = entrada.Miniatura,
        };
    }
}
