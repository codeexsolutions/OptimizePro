namespace OptimizePro.Central;

public interface IChaveDeMaquinaRepository
{
    Task<ChaveDeMaquina?> ObterAsync(string instalacaoId, string maquinaId, CancellationToken ct = default);

    /// <summary>Todas as chaves válidas de uma instalação — autenticar precisa conferir contra qualquer máquina dela, não uma só.</summary>
    Task<List<ChaveDeMaquina>> ListarDaInstalacaoAsync(string instalacaoId, CancellationToken ct = default);

    Task CriarAsync(ChaveDeMaquina chave, CancellationToken ct = default);
}
