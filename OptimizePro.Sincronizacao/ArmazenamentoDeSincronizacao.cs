using System.Security.Cryptography;
using System.Text.Json;
using OptimizePro.Services.Armazenamento;

namespace OptimizePro.Sincronizacao;

public sealed record EstadoLocalDeSincronizacao(string InstalacaoId, string ChaveDeApi);

/// <summary>
/// Guarda o InstalacaoId+ChaveDeApi desta instalação (§24.2) — mesmo mecanismo (DPAPI,
/// amarrado ao usuário do Windows) que <c>LicencaService</c> usa pro estado da licença, pelo
/// mesmo motivo: é um segredo local que não devia sobreviver a uma cópia crua do arquivo pra
/// outra máquina/usuário.
/// </summary>
public sealed class ArmazenamentoDeSincronizacao
{
    private static readonly byte[] Entropia = "OptimizePro.Sincronizacao.v1"u8.ToArray();

    private readonly string _arquivoDeEstado;

    public ArmazenamentoDeSincronizacao(CaminhosDoApp caminhos) : this(caminhos.ArquivoDeSincronizacao) { }

    internal ArmazenamentoDeSincronizacao(string arquivoDeEstado) => _arquivoDeEstado = arquivoDeEstado;

    public EstadoLocalDeSincronizacao? Ler()
    {
        if (!File.Exists(_arquivoDeEstado)) return null;

        try
        {
            var protegido = File.ReadAllBytes(_arquivoDeEstado);
            var json = ProtectedData.Unprotect(protegido, Entropia, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<EstadoLocalDeSincronizacao>(json);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or IOException)
        {
            // Arquivo corrompido/de outra máquina — trata como "nunca provisionou"; a próxima
            // sincronização reprovisiona (idempotente do lado da Central) e resolve sozinho.
            return null;
        }
    }

    public void Salvar(EstadoLocalDeSincronizacao estado)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivoDeEstado)!);
        var json = JsonSerializer.SerializeToUtf8Bytes(estado);
        var protegido = ProtectedData.Protect(json, Entropia, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_arquivoDeEstado, protegido);
    }

    /// <summary>ID desta máquina física (§24.1) — arquivo-irmão do estado de sincronização, sem DPAPI porque não é segredo, só precisa ser estável.</summary>
    public string ObterOuCriarIdentidadeDaMaquina() =>
        IdentidadeDaMaquina.ObterOuCriar(Path.Combine(Path.GetDirectoryName(_arquivoDeEstado) ?? ".", "maquina.dat"));
}
