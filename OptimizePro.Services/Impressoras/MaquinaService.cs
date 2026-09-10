using OptimizePro.Data.Entidades;
using OptimizePro.Data.Repositorios;

namespace OptimizePro.Services.Impressoras;

public sealed class MaquinaService(IMaquinaRepository repositorio, GerenciadorDeVarredura gerenciador) : IMaquinaService
{
    public Task<List<Maquina>> ListarAsync(bool incluirDesabilitadas, CancellationToken ct = default) =>
        repositorio.ListarAsync(incluirDesabilitadas, ct);

    public EstadoDaVarredura ObterEstadoDaVarredura() => gerenciador.ObterEstado();

    public event Action<EstadoDaVarredura>? VarreduraAtualizada
    {
        add => gerenciador.Atualizado += value;
        remove => gerenciador.Atualizado -= value;
    }

    public bool IniciarVarredura(IReadOnlyList<string> hosts) => gerenciador.Iniciar(hosts);

    public void PararVarredura() => gerenciador.Parar();

    public async Task<Maquina?> CadastrarPendenteAsync(string host, string nome, CancellationToken ct = default)
    {
        var achada = gerenciador.ObterPendente(host);
        if (achada is null) return null;
        if (await repositorio.ObterPorHostAsync(host, ct) is not null)
        {
            gerenciador.RegistrarCadastro(host, "", ""); // já existia — só limpa a pendência
            return null;
        }

        var existentes = await repositorio.ListarAsync(incluirDesabilitadas: true, ct);
        var idsUsados = existentes.Select(m => m.Id).ToHashSet();
        var (id, _) = VarreduraDeRedeService.SugerirIdentidade(host, idsUsados);

        var maquina = new Maquina
        {
            Id = id,
            Nome = nome,
            Tipo = achada.Tipo,
            Habilitada = true,
            Host = achada.Host,
            Ip = achada.Ip,
            CaminhoHistorico = achada.CaminhoHistorico,
            PastaPreview = achada.PastaPreview,
            PastaLogAoVivo = achada.PastaLogAoVivo,
            ArquivoLogAoVivo = achada.ArquivoLogAoVivo,
            PastaLogDeStatus = achada.PastaLogDeStatus,
            CaminhoListaDeTrabalhos = achada.CaminhoListaDeTrabalhos,
            CaminhoEstatisticasDeTinta = achada.CaminhoEstatisticasDeTinta,
            Origem = "scan",
            Posicao = existentes.Count,
            DescobertaEm = DateTime.UtcNow,
        };

        var salva = await repositorio.SalvarAsync(maquina, ct);
        gerenciador.RegistrarCadastro(host, salva.Id, salva.Nome);
        return salva;
    }

    public bool DescartarPendente(string host) => gerenciador.Descartar(host);

    public Task<bool> DesativarAsync(string id, CancellationToken ct = default) =>
        repositorio.AtualizarHabilitadaAsync(id, false, ct);

    public Task<bool> ReativarAsync(string id, CancellationToken ct = default) =>
        repositorio.AtualizarHabilitadaAsync(id, true, ct);

    public async Task<(bool Ok, string? Erro)> ExcluirAsync(string id, CancellationToken ct = default)
    {
        var maquina = await repositorio.ObterAsync(id, ct);
        if (maquina is null) return (false, "Máquina não encontrada");
        if (maquina.Habilitada) return (false, "Desative a máquina antes de excluir");

        await repositorio.ExcluirAsync(id, ct);
        return (true, null);
    }
}
