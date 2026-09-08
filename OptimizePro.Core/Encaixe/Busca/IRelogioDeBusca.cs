using System.Diagnostics;

namespace OptimizePro.Core.Encaixe.Busca;

/// <summary>Abstrai o tempo decorrido — permite testar a busca (parede/perseguir) sem depender de tempo real.</summary>
public interface IRelogioDeBusca
{
    long TempoDecorridoMs { get; }
}

public sealed class RelogioReal : IRelogioDeBusca
{
    private readonly Stopwatch _cronometro = Stopwatch.StartNew();

    public long TempoDecorridoMs => _cronometro.ElapsedMilliseconds;
}
