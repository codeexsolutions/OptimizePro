using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OptimizePro.Data;
using OptimizePro.Painel;
using OptimizePro.Services.Licenciamento;

namespace OptimizePro.Sincronizacao;

/// <summary>
/// Junta os dados locais (chão de fábrica + painel) e empurra pra Central (§24.2). Sempre
/// best-effort: sem licença liberada, sem internet, ou com a Central fora do ar, simplesmente
/// não sincroniza nada nesta rodada — nunca lança, nunca trava o app desktop. Quem chama isto
/// de tempos em tempos é um <c>BackgroundService</c> (a entrar no host do app, fora deste
/// projeto — este projeto só sabe COMO sincronizar, não QUANDO).
/// </summary>
public sealed class SincronizacaoService(
    OptimizeDbContext operacionalDb,
    PainelDbContext painelDb,
    LicencaService licenca,
    ArmazenamentoDeSincronizacao armazenamento,
    IClienteCentralHttp cliente)
{
    // Histórico sincronizado é pra dar contexto recente no dashboard remoto, não é arquivo
    // morto — sem este corte, o lote cresceria sem limite pra sempre a cada ciclo periódico
    // (§24.2, decisão consciente de escopo; incremental/delta fica pra quando doer de verdade).
    private const int DiasDeHistoricoSincronizados = 90;

    public async Task<bool> SincronizarAsync(CancellationToken ct = default)
    {
        if (!cliente.Configurado) return false;

        var estado = await GarantirProvisionadoAsync(ct);
        if (estado is null) return false;

        var itens = new List<ItemParaSincronizar>();
        itens.AddRange(await ColetarMaquinasAsync(ct));
        itens.AddRange(await ColetarHistoricoAsync(ct));
        itens.AddRange(await ColetarPedidosAsync(ct));
        itens.AddRange(await ColetarOrdensDeServicoAsync(ct));
        itens.AddRange(await ColetarFaturamentoAsync(ct));

        var envioOk = itens.Count == 0 || await cliente.EnviarLoteAsync(estado.InstalacaoId, estado.ChaveDeApi, itens, ct);

        // Pull de Usuario, na direção oposta do resto (§25) — desde a §24.7 a Central manda em
        // Usuario, então o app desktop puxa o estado atual pra um cache local (mesma tabela
        // Usuarios do §23.1, agora só espelho) e usa isso pra logar gente OFFLINE no chão de
        // fábrica, sem depender de rede na hora do login. Falha aqui não deve derrubar o
        // retorno geral do ciclo — é um passo independente, best-effort à parte.
        await PuxarUsuariosAsync(estado, ct);

        return envioOk;
    }

    private async Task<EstadoLocalDeSincronizacao?> GarantirProvisionadoAsync(CancellationToken ct)
    {
        var existente = armazenamento.Ler();
        if (existente is not null) return existente;

        var estadoDaLicenca = licenca.ObterEstado();
        if (!estadoDaLicenca.Liberado || estadoDaLicenca.ClienteIdHash is not { } clienteIdHash)
            return null; // sem licença ativa não tem ClienteIdHash — nada pra provisionar ainda.

        var resposta = await cliente.ProvisionarAsync(clienteIdHash, null, ct);

        // "jaExistia sem chave nova" é um gap conhecido (ex.: reinstalação perdeu o arquivo
        // local, mas a Central já tinha esse ClienteIdHash) — sem uma rota de "regenerar
        // chave" na Central, não tem como recuperar sozinho aqui. Fica pendente pra depois.
        if (resposta?.ChaveDeApi is null) return null;

        var novoEstado = new EstadoLocalDeSincronizacao(resposta.InstalacaoId, resposta.ChaveDeApi);
        armazenamento.Salvar(novoEstado);
        return novoEstado;
    }

    private async Task<List<ItemParaSincronizar>> ColetarMaquinasAsync(CancellationToken ct)
    {
        var maquinas = await operacionalDb.Maquinas.AsNoTracking()
            .Select(m => new MaquinaDto(m.Id, m.Nome, m.Tipo.ToString(), m.Habilitada, m.Host, m.Ip))
            .ToListAsync(ct);

        return maquinas.Select(m => Empacotar(TipoDeItem.Maquina, m.Id, m)).ToList();
    }

    private async Task<List<ItemParaSincronizar>> ColetarHistoricoAsync(CancellationToken ct)
    {
        var dataDeCorte = DateTime.UtcNow.AddDays(-DiasDeHistoricoSincronizados).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var registros = await operacionalDb.RegistrosDeImpressao.AsNoTracking()
            .Where(r => r.Data.CompareTo(dataDeCorte) >= 0)
            .Select(r => new RegistroDeImpressaoDto(
                r.Id, r.MaquinaId, r.NomeDaMaquina, r.DataHora, r.Data, r.Tarefa,
                r.AreaDeImpressao, r.ComprimentoDeImpressao, r.Status, r.Cancelada, r.ComErro, r.TintaMl))
            .ToListAsync(ct);

        return registros.Select(r => Empacotar(TipoDeItem.RegistroDeImpressao, r.Id, r)).ToList();
    }

    private async Task<List<ItemParaSincronizar>> ColetarPedidosAsync(CancellationToken ct)
    {
        var pedidos = await operacionalDb.Pedidos.AsNoTracking()
            .Select(p => new PedidoDto(
                p.Id, p.CriadoEm, p.Status, p.Observacao,
                p.Itens.OrderBy(i => i.Posicao).Select(i => new PedidoItemDto(
                    i.Id, i.Posicao, i.RegistroId, i.NomeDoCliente, i.Tecido, i.Tarefa,
                    i.NomeDaMaquina, i.ComprimentoDeImpressao, i.Data, i.StatusNaCalandra)).ToList()))
            .ToListAsync(ct);

        return pedidos.Select(p => Empacotar(TipoDeItem.Pedido, p.Id, p)).ToList();
    }

    private async Task<List<ItemParaSincronizar>> ColetarOrdensDeServicoAsync(CancellationToken ct)
    {
        var ordens = await operacionalDb.OrdensDeServico.AsNoTracking()
            .Select(o => new OrdemDeServicoDto(
                o.Id, o.NomeDoCliente, o.Tecido, o.TamanhoDeImpressao, o.Metros,
                o.Operador, o.Maquina, o.Data, o.Observacao, o.Imagens.Count))
            .ToListAsync(ct);

        return ordens.Select(o => Empacotar(TipoDeItem.OrdemDeServico, o.Id, o)).ToList();
    }

    private async Task<List<ItemParaSincronizar>> ColetarFaturamentoAsync(CancellationToken ct)
    {
        var configuracao = await painelDb.ConfiguracoesDeFaturamento.AsNoTracking().FirstOrDefaultAsync(c => c.Id == 1, ct);
        if (configuracao is null) return [];

        var dto = new FaturamentoDto(
            configuracao.ValorBaseMensal, configuracao.ValorPorUsuarioExtra, configuracao.LimiteDeUsuariosNoPlano,
            licenca.ObterEstado().ValidoAte);
        return [Empacotar(TipoDeItem.Faturamento, "1", dto)];
    }

    private async Task PuxarUsuariosAsync(EstadoLocalDeSincronizacao estado, CancellationToken ct)
    {
        var usuarios = await cliente.ObterUsuariosAsync(estado.InstalacaoId, estado.ChaveDeApi, ct);
        if (usuarios is null) return; // offline/erro — mantém o cache local antigo como está.

        // Espelho completo, não upsert incremental: a tabela é pequena (poucas dezenas de
        // linhas no máximo) e a Central sempre manda o estado inteiro, então "apagar tudo e
        // recriar" é mais simples e não deixa usuário excluído remotamente sobrando aqui.
        // Duas SaveChanges separadas (delete, depois insert) evita depender da ordem de
        // execução do EF quando delete e insert reusam o mesmo Id de linha.
        var atuais = await painelDb.Usuarios.ToListAsync(ct);
        if (atuais.Count > 0)
        {
            painelDb.Usuarios.RemoveRange(atuais);
            await painelDb.SaveChangesAsync(ct);
        }

        if (usuarios.Count > 0)
        {
            painelDb.Usuarios.AddRange(usuarios.Select(ParaUsuarioLocal));
            await painelDb.SaveChangesAsync(ct);
        }
    }

    private static Usuario ParaUsuarioLocal(UsuarioDto dto) => new()
    {
        Id = dto.Id,
        Login = dto.Login,
        Nome = dto.Nome,
        SenhaHash = dto.SenhaHash,
        SenhaSal = dto.SenhaSal,
        EhAdministrador = dto.EhAdministrador,
        Habilitado = dto.Habilitado,
        // Ignora valor desconhecido em vez de lançar — um módulo com nome que o desktop não
        // reconhece (versão desatualizada, por exemplo) não deve quebrar o cache inteiro.
        ModulosLiberados = dto.ModulosLiberados
            .Select(m => Enum.TryParse<ModuloDoPainel>(m, out var modulo) ? modulo : (ModuloDoPainel?)null)
            .Where(m => m is not null)
            .Select(m => m!.Value)
            .ToList(),
        CriadoEm = DateTime.UtcNow,
    };

    private static ItemParaSincronizar Empacotar<T>(string tipo, string entidadeId, T dados) =>
        new(tipo, entidadeId, JsonSerializer.Serialize(dados, (JsonSerializerOptions?)null), DateTime.UtcNow);
}
