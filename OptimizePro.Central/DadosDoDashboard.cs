namespace OptimizePro.Central;

// Mesma forma de OptimizePro.Sincronizacao.Dtos.cs — é o que chega dentro de
// DadoSincronizado.DadosJson pra cada tipo. Duplicado de propósito (isolamento §23/§24), só o
// contrato do JSON (§24.6, telas de dashboard).

public sealed record MaquinaDto(string Id, string Nome, string Tipo, bool Habilitada, string? Host, string? Ip);

public sealed record RegistroDeImpressaoDto(
    string Id, string MaquinaId, string? NomeDaMaquina, string DataHora, string Data, string? Tarefa,
    double AreaDeImpressao, double ComprimentoDeImpressao, string? Status, bool Cancelada, bool ComErro, double TintaMl);

public sealed record PedidoItemDto(
    string Id, int Posicao, string RegistroId, string? NomeDoCliente, string? Tecido, string? Tarefa,
    string? NomeDaMaquina, double? ComprimentoDeImpressao, string? Data, string StatusNaCalandra);

public sealed record PedidoDto(string Id, DateTime CriadoEm, string Status, string? Observacao, List<PedidoItemDto> Itens);

public sealed record OrdemDeServicoDto(
    string Id, string NomeDoCliente, string? Tecido, string? TamanhoDeImpressao, double? Metros,
    string? Operador, string? Maquina, string Data, string? Observacao, int QuantidadeDeImagens);
