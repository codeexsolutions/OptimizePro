namespace OptimizePro.Licenciamento;

/// <summary>Só pra rótulo/mensagem na tela — não muda a verificação em si (mesma assinatura digital vale pra ambos).</summary>
public enum TipoDeLicenca : byte
{
    Paga = 0,
    Teste = 1,
}

/// <summary>O que vem de dentro de um código de licença válido, depois de verificar a assinatura.</summary>
public sealed record InformacoesDaLicenca(DateOnly ValidoAte, uint ClienteIdHash, TipoDeLicenca Tipo);
