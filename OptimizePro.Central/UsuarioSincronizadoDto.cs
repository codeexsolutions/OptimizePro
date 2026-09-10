namespace OptimizePro.Central;

/// <summary>
/// Mesma forma de <c>OptimizePro.Sincronizacao.UsuarioDto</c> — é o que chega dentro de
/// <see cref="DadoSincronizado.DadosJson"/> quando <c>Tipo == TipoDeDadoSincronizado.Usuario</c>.
/// Duplicado de propósito (isolamento §23/§24), é só o contrato do JSON, não um tipo
/// compartilhado.
/// </summary>
public sealed record UsuarioSincronizadoDto(
    string Id, string Login, string Nome, bool EhAdministrador, bool Habilitado,
    List<string> ModulosLiberados, byte[] SenhaHash, byte[] SenhaSal);
