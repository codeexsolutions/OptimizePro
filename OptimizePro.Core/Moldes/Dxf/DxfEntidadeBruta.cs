using System.Globalization;

namespace OptimizePro.Core.Moldes.Dxf;

/// <summary>
/// Uma entidade DXF ainda "crua": tipo (ex.: "LINE") + seus pares código/valor, na
/// ordem em que apareceram no arquivo (importante para reconstruir vértices de
/// LWPOLYLINE/SPLINE, onde o mesmo código repete por vértice). <see cref="Filhos"/>
/// é usado só por POLYLINE (vértices) e BLOCK (entidades contidas).
/// </summary>
public sealed class DxfEntidadeBruta(string tipo)
{
    public string Tipo { get; } = tipo;

    public List<DxfPar> Grupos { get; } = [];

    public List<DxfEntidadeBruta> Filhos { get; } = [];

    public string? Primeiro(int codigo)
    {
        foreach (var grupo in Grupos)
        {
            if (grupo.Codigo == codigo)
                return grupo.Valor;
        }
        return null;
    }

    public IEnumerable<string> Todos(int codigo) => Grupos.Where(g => g.Codigo == codigo).Select(g => g.Valor);

    public double PrimeiroDouble(int codigo, double padrao = 0)
    {
        var valor = Primeiro(codigo);
        return valor is not null && double.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : padrao;
    }

    public int PrimeiroInt(int codigo, int padrao = 0)
    {
        var valor = Primeiro(codigo);
        return valor is not null && int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
            ? i
            : padrao;
    }
}
