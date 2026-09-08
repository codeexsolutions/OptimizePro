namespace OptimizePro.Core.Moldes.Plt;

/// <summary>
/// Estado do "plotter" enquanto os comandos HP-GL são processados: posição atual, modo
/// absoluto/relativo (PA/PR) e o traço em construção (fechado quando a pena levanta).
/// </summary>
internal sealed class PltDesenhista
{
    private readonly List<Traco> _tracos = [];
    private readonly List<PontoXY> _tracoAtual = [];

    public PontoXY Posicao { get; private set; } = new(0, 0);

    public bool ModoAbsoluto { get; set; } = true;

    public bool PenaAbaixada { get; private set; }

    public IReadOnlyList<Traco> Tracos => _tracos;

    public void DefinirPena(bool abaixada) => PenaAbaixada = abaixada;

    /// <summary>Move interpretando <paramref name="coordenadas"/> conforme <see cref="ModoAbsoluto"/> (PU/PD/PA/PR).</summary>
    public void MoverParaComModo(PontoXY coordenadas, bool desenhando)
    {
        var destino = ModoAbsoluto
            ? coordenadas
            : new PontoXY(Posicao.X + coordenadas.X, Posicao.Y + coordenadas.Y);

        DesenharSegmentoAbsoluto(destino, desenhando);
    }

    /// <summary>Move por um delta SEMPRE relativo, independente de <see cref="ModoAbsoluto"/> (usado pelo PE).</summary>
    public void MoverRelativo(PontoXY delta, bool desenhando) =>
        DesenharSegmentoAbsoluto(new PontoXY(Posicao.X + delta.X, Posicao.Y + delta.Y), desenhando);

    public void DesenharSegmentoAbsoluto(PontoXY destino, bool desenhando)
    {
        if (desenhando)
        {
            if (_tracoAtual.Count == 0)
                _tracoAtual.Add(Posicao);
            _tracoAtual.Add(destino);
        }
        else
        {
            FinalizarTracoAtual();
        }

        Posicao = destino;
    }

    /// <summary>Adiciona um laço fechado independente (ex.: CI) sem alterar a posição atual (pena volta ao centro).</summary>
    public void AdicionarTracoFechado(IReadOnlyList<PontoXY> pontos)
    {
        FinalizarTracoAtual();
        _tracos.Add(new Traco(pontos, true));
    }

    public void FinalizarTracoAtual()
    {
        if (_tracoAtual.Count >= 2)
            _tracos.Add(new Traco([.. _tracoAtual], false));
        _tracoAtual.Clear();
    }
}
