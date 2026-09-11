namespace OptimizePro.Sincronizacao;

// Formas enxutas dos dados locais pra Central (§24.2) — nunca as entidades do EF direto: além
// de evitar vazar navegações internas, corta de propósito o que não devia ir num JSON
// sincronizado a cada poucos minutos (ex.: os bytes da imagem de uma OS — ver ColetarOrdensDeServicoAsync).

public sealed record MaquinaDto(string Id, string Nome, string Tipo, bool Habilitada, string? Host, string? Ip);

public sealed record RegistroDeImpressaoDto(
    string Id, string MaquinaId, string? NomeDaMaquina, string DataHora, string Data, string? Tarefa,
    double AreaDeImpressao, double ComprimentoDeImpressao, string? Status, bool Cancelada, bool ComErro, double TintaMl);

public sealed record PedidoItemDto(
    string Id, int Posicao, string RegistroId, string? NomeDoCliente, string? Tecido, string? Tarefa,
    string? NomeDaMaquina, double? ComprimentoDeImpressao, string? Data, string StatusNaCalandra);

public sealed record PedidoDto(string Id, DateTime CriadoEm, string Status, string? Observacao, List<PedidoItemDto> Itens);

/// <summary>Sem as imagens em si — só a contagem. Sincronizar bytes de imagem a cada ciclo periódico é caro demais pra este primeiro corte; entra quando o dashboard remoto precisar mostrá-las de verdade, com endpoint próprio.</summary>
public sealed record OrdemDeServicoDto(
    string Id, string NomeDoCliente, string? Tecido, string? TamanhoDeImpressao, double? Metros,
    string? Operador, string? Maquina, string Data, string? Observacao, int QuantidadeDeImagens);

public sealed record UsuarioDto(string Id, string Login, string Nome, bool EhAdministrador, bool Habilitado, List<string> ModulosLiberados, byte[] SenhaHash, byte[] SenhaSal);

/// <summary>
/// <c>LicencaValidaAte</c> viaja aqui (não é dado de faturamento local — vem de
/// <c>LicencaService.ObterEstado().ValidoAte</c>, outro projeto) porque este é o tipo que já
/// sincroniza a cada ciclo periódico; criar um tipo `TipoDeItem` só pra uma data seria mais
/// uma entidade sincronizada pra uma informação que sempre anda junto da tela de Faturamento
/// (§24.8 — "vence em").
/// </summary>
public sealed record FaturamentoDto(decimal ValorBaseMensal, decimal ValorPorUsuarioExtra, int LimiteDeUsuariosNoPlano, DateOnly? LicencaValidaAte);
