namespace OptimizePro.Core.Moldes.Dxf;

public sealed class DxfDocumento
{
    public Dictionary<string, string> VariaveisDeHeader { get; } = [];

    /// <summary>Nome do bloco → entidade BLOCK (com o conteúdo em <see cref="DxfEntidadeBruta.Filhos"/>).</summary>
    public Dictionary<string, DxfEntidadeBruta> Blocos { get; } = [];

    public List<DxfEntidadeBruta> EntidadesTopo { get; } = [];
}
