using System.Text.Json;

namespace OptimizePro.Central;

public sealed class UsuarioAdminService(IDadoSincronizadoRepository dados) : IUsuarioAdminService
{
    public async Task<List<UsuarioSincronizadoDto>> ListarAsync(string instalacaoId, CancellationToken ct = default)
    {
        var linhas = await dados.ListarPorTipoAsync(instalacaoId, TipoDeDadoSincronizado.Usuario, ct);
        var lista = new List<UsuarioSincronizadoDto>();
        foreach (var linha in linhas)
        {
            if (Desserializar(linha.DadosJson) is { } usuario) lista.Add(usuario);
        }
        return lista.OrderBy(u => u.Nome).ToList();
    }

    public async Task<UsuarioSincronizadoDto> CadastrarAsync(string instalacaoId, string login, string nome, string senha, IReadOnlyList<string> modulosLiberados, bool ehAdministrador, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(login)) throw new ArgumentException("Informe o login.", nameof(login));
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Informe o nome.", nameof(nome));
        if (string.IsNullOrWhiteSpace(senha)) throw new ArgumentException("Informe a senha.", nameof(senha));

        var loginNormalizado = login.Trim();
        var existentes = await ListarAsync(instalacaoId, ct);
        if (existentes.Any(u => u.Login == loginNormalizado))
            throw new LoginJaExisteException(loginNormalizado);

        var (hash, sal) = HashDeSenha.Gerar(senha);
        var usuario = new UsuarioSincronizadoDto(
            Guid.NewGuid().ToString(), loginNormalizado, nome.Trim(), ehAdministrador, true,
            modulosLiberados.ToList(), hash, sal);

        await dados.SalvarAsync(instalacaoId, Empacotar(usuario), ct);
        return usuario;
    }

    private static readonly string[] TodosOsModulos = ["Impressoras", "Maquinas", "Historico", "Reposicao", "Pedidos", "OrdensDeServico"];

    public async Task<UsuarioSincronizadoDto> BootstrapAsync(string instalacaoId, string login, string nome, string senha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(login)) throw new ArgumentException("Informe o login.", nameof(login));
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Informe o nome.", nameof(nome));
        if (string.IsNullOrWhiteSpace(senha)) throw new ArgumentException("Informe a senha.", nameof(senha));

        if ((await ListarAsync(instalacaoId, ct)).Count > 0)
            throw new JaTemUsuarioException();

        var (hash, sal) = HashDeSenha.Gerar(senha);
        var usuario = new UsuarioSincronizadoDto(
            Guid.NewGuid().ToString(), login.Trim(), nome.Trim(), true, true, TodosOsModulos.ToList(), hash, sal);

        await dados.SalvarAsync(instalacaoId, Empacotar(usuario), ct);
        return usuario;
    }

    public async Task<bool> AtualizarAsync(string instalacaoId, string id, string nome, IReadOnlyList<string> modulosLiberados, bool ehAdministrador, CancellationToken ct = default)
    {
        var atual = await ObterAsync(instalacaoId, id, ct);
        if (atual is null) return false;

        var atualizado = atual with { Nome = nome.Trim(), ModulosLiberados = modulosLiberados.ToList(), EhAdministrador = ehAdministrador };
        await dados.SalvarAsync(instalacaoId, Empacotar(atualizado), ct);
        return true;
    }

    public async Task<bool> RedefinirSenhaAsync(string instalacaoId, string id, string novaSenha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(novaSenha)) throw new ArgumentException("Informe a nova senha.", nameof(novaSenha));

        var atual = await ObterAsync(instalacaoId, id, ct);
        if (atual is null) return false;

        var (hash, sal) = HashDeSenha.Gerar(novaSenha);
        var atualizado = atual with { SenhaHash = hash, SenhaSal = sal };
        await dados.SalvarAsync(instalacaoId, Empacotar(atualizado), ct);
        return true;
    }

    public async Task<bool> AtualizarHabilitadoAsync(string instalacaoId, string id, bool habilitado, CancellationToken ct = default)
    {
        var atual = await ObterAsync(instalacaoId, id, ct);
        if (atual is null) return false;

        var atualizado = atual with { Habilitado = habilitado };
        await dados.SalvarAsync(instalacaoId, Empacotar(atualizado), ct);
        return true;
    }

    public Task<bool> ExcluirAsync(string instalacaoId, string id, CancellationToken ct = default) =>
        dados.ExcluirAsync(instalacaoId, TipoDeDadoSincronizado.Usuario, id, ct);

    private async Task<UsuarioSincronizadoDto?> ObterAsync(string instalacaoId, string id, CancellationToken ct)
    {
        var linha = await dados.ObterAsync(instalacaoId, TipoDeDadoSincronizado.Usuario, id, ct);
        return linha is null ? null : Desserializar(linha.DadosJson);
    }

    private static UsuarioSincronizadoDto? Desserializar(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<UsuarioSincronizadoDto>(json);
        }
        catch (JsonException)
        {
            return null; // linha corrompida/formato antigo — não derruba a listagem inteira.
        }
    }

    private static ItemSincronizado Empacotar(UsuarioSincronizadoDto usuario) =>
        new(TipoDeDadoSincronizado.Usuario, usuario.Id, JsonSerializer.Serialize(usuario, (JsonSerializerOptions?)null), DateTime.UtcNow);
}
