using System.Text.Json;

namespace OptimizePro.Central;

public sealed class AutenticacaoDeUsuarioService(IInstalacaoRepository instalacoes, IDadoSincronizadoRepository dados) : IAutenticacaoDeUsuarioService
{
    public async Task<ResultadoDoLoginRemoto> AutenticarAsync(string codigoDaInstalacao, string login, string senha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigoDaInstalacao) || string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(senha))
            return new ResultadoDoLoginRemoto(false, null, "Informe a empresa, o login e a senha.");

        var instalacao = await instalacoes.ObterPorCodigoAsync(codigoDaInstalacao.Trim().ToUpperInvariant(), ct);
        // Mesma mensagem genérica pra empresa/login/senha errados — não dar pista de qual dos
        // três está errado (mesmo raciocínio de OptimizePro.Painel.AutenticacaoService).
        const string erroGenerico = "Empresa, login ou senha inválidos.";
        if (instalacao is null) return new ResultadoDoLoginRemoto(false, null, erroGenerico);

        var usuarios = await dados.ListarPorTipoAsync(instalacao.Id, TipoDeDadoSincronizado.Usuario, ct);

        UsuarioSincronizadoDto? encontrado = null;
        foreach (var linha in usuarios)
        {
            UsuarioSincronizadoDto? candidato;
            try
            {
                candidato = JsonSerializer.Deserialize<UsuarioSincronizadoDto>(linha.DadosJson);
            }
            catch (JsonException)
            {
                continue; // linha corrompida/formato antigo — ignora, não derruba o login de ninguém.
            }

            if (candidato is not null && candidato.Login == login) { encontrado = candidato; break; }
        }

        if (encontrado is null || !HashDeSenha.Conferir(senha, encontrado.SenhaHash, encontrado.SenhaSal))
            return new ResultadoDoLoginRemoto(false, null, erroGenerico);

        if (!encontrado.Habilitado)
            return new ResultadoDoLoginRemoto(false, null, "Este usuário está desativado.");

        return new ResultadoDoLoginRemoto(true, new UsuarioAutenticado(
            instalacao.Id, encontrado.Id, encontrado.Login, encontrado.Nome, encontrado.EhAdministrador, encontrado.ModulosLiberados), null);
    }
}
