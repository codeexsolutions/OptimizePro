using OptimizePro.Sincronizacao;

namespace OptimizePro.Sincronizacao.Tests;

public sealed class ClienteCentralFalso : IClienteCentralHttp
{
    public bool Configurado { get; set; } = true;

    public RespostaDeProvisionamento? ProximaRespostaDeProvisionamento { get; set; }
    public bool ProximoResultadoDeEnvio { get; set; } = true;
    public List<UsuarioDto>? ProximaListaDeUsuarios { get; set; }

    public int ChamadasDeProvisionar { get; private set; }
    public int ChamadasDeEnvio { get; private set; }
    public int ChamadasDeObterUsuarios { get; private set; }
    public IReadOnlyList<ItemParaSincronizar>? UltimoLoteEnviado { get; private set; }
    public (string InstalacaoId, string ChaveDeApi)? UltimasCredenciaisUsadas { get; private set; }

    public Task<RespostaDeProvisionamento?> ProvisionarAsync(uint clienteIdHash, string maquinaId, string? nomeDaFabrica, CancellationToken ct = default)
    {
        ChamadasDeProvisionar++;
        return Task.FromResult(ProximaRespostaDeProvisionamento);
    }

    public Task<bool> EnviarLoteAsync(string instalacaoId, string chaveDeApi, IReadOnlyList<ItemParaSincronizar> itens, CancellationToken ct = default)
    {
        ChamadasDeEnvio++;
        UltimoLoteEnviado = itens;
        UltimasCredenciaisUsadas = (instalacaoId, chaveDeApi);
        return Task.FromResult(ProximoResultadoDeEnvio);
    }

    public Task<List<UsuarioDto>?> ObterUsuariosAsync(string instalacaoId, string chaveDeApi, CancellationToken ct = default)
    {
        ChamadasDeObterUsuarios++;
        return Task.FromResult(ProximaListaDeUsuarios);
    }
}
