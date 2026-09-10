using System.Security.Cryptography;
using System.Text.Json;
using OptimizePro.Licenciamento;
using OptimizePro.Services.Armazenamento;

namespace OptimizePro.Services.Licenciamento;

public enum SituacaoDaLicenca
{
    /// <summary>Nunca ativou nenhum código nesta instalação (ou o arquivo salvo está corrompido/ilegível).</summary>
    NuncaAtivada,
    Valida,
    Expirada,

    /// <summary>O relógio do sistema voltou pra uma data ANTERIOR a uma já vista por esta instalação — sinal de tentativa de burlar a validade adiando o vencimento (§ "código mensal", pedido do usuário). Trata como bloqueado até reativar.</summary>
    RelogioSuspeito,
}

public sealed record EstadoDaLicenca(SituacaoDaLicenca Situacao, DateOnly? ValidoAte, TipoDeLicenca? Tipo, uint? ClienteIdHash = null)
{
    public bool Liberado => Situacao == SituacaoDaLicenca.Valida;
}

public sealed record ResultadoDaAtivacao(bool Sucesso, string Mensagem, EstadoDaLicenca? Estado);

/// <summary>
/// Controle de acesso por código de licença (pedido do usuário, 02/09/2026) — "cliente recebe
/// um código mensal, informa ao sistema; se for válido continua usando, senão nem acessa".
/// Escolhido pra funcionar 100% OFFLINE (o cliente pode desligar a internet e usar o mês
/// inteiro sem bloqueio nenhum — decisão explícita do usuário) — por isso a validade não
/// depende de nenhuma chamada de rede, só de uma assinatura digital (ECDSA P-256) que o app
/// verifica sozinho com a chave PÚBLICA embutida aqui. A chave PRIVADA (que gera os códigos)
/// fica só na ferramenta separada <c>Ferramentas/GeradorDeLicenca</c>, nunca neste projeto.
/// </summary>
/// <remarks>
/// Sendo 100% offline, não tem como confiar cegamente no relógio do Windows (o cliente podia
/// atrasar a data pra "reviver" um código vencido) nem como REVOGAR um código antes do
/// vencimento dele (sem internet, o app não tem como saber que o cliente parou de pagar no
/// meio do mês) — os dois são limites conscientes da escolha "100% offline", não bugs.
/// A defesa possível sem internet: guardar a MAIOR data que esta instalação já viu rodando e
/// desconfiar se o relógio atual vier ANTES dela (<see cref="SituacaoDaLicenca.RelogioSuspeito"/>).
/// Como qualquer trava só-de-software, isto não é inquebrável contra alguém disposto a
/// decompilar o app ou editar o arquivo de estado byte a byte — é uma barreira prática contra
/// uso casual sem pagar, no mesmo nível de chave de produto de software comercial comum.
/// </remarks>
public sealed class LicencaService
{
    /// <summary>
    /// Chave PÚBLICA (SubjectPublicKeyInfo, Base64) — só CONFERE assinatura, não GERA. Gerada
    /// junto com a chave privada por <c>GeradorDeLicenca gerar-chave</c>; trocar aqui invalida
    /// todo código já emitido com a chave antiga (só troque junto com uma migração planejada).
    /// </summary>
    private const string ChavePublicaBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEPkgzpFF8sWdclY7ydb2m8iUzFnHoXtiJvcrBHbRH3U/+i0Am98uYSRoVMPcnZP4nOs69mkvLrQB9zX1fV1THXA==";

    /// <summary>Só pra <see cref="ProtectedData"/> (DPAPI) amarrar o arquivo ao USUÁRIO do Windows que ativou — outro usuário da mesma máquina não lê o estado salvo, mas isso é só uma camada a mais, não a defesa principal (essa é a assinatura).</summary>
    private static readonly byte[] Entropia = "OptimizePro.Licenciamento.v1"u8.ToArray();

    private readonly string _arquivoDeEstado;

    public LicencaService(CaminhosDoApp caminhos) : this(caminhos.ArquivoDeLicenca) { }

    internal LicencaService(string arquivoDeEstado) => _arquivoDeEstado = arquivoDeEstado;

    private sealed record EstadoPersistido(string Codigo, DateOnly MaiorDataJaVista);

    public EstadoDaLicenca ObterEstado()
    {
        var persistido = LerEstadoPersistido();
        if (persistido is null)
            return new EstadoDaLicenca(SituacaoDaLicenca.NuncaAtivada, null, null);

        // Sempre reverifica a assinatura do código SALVO — a validade nunca vem dos campos
        // derivados do arquivo (esses só existem pra não ter que pedir o código de novo a cada
        // abertura), sempre da assinatura em si. Editar o arquivo sem a chave privada não cola.
        var info = CodificadorDeLicenca.Verificar(persistido.Codigo, ObterChavePublica());
        if (info is null)
            return new EstadoDaLicenca(SituacaoDaLicenca.NuncaAtivada, null, null);

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        if (hoje < persistido.MaiorDataJaVista)
            return new EstadoDaLicenca(SituacaoDaLicenca.RelogioSuspeito, info.ValidoAte, info.Tipo, info.ClienteIdHash);

        // Marca d'água sobe com o tempo — pega tentativa de atrasar o relógio a partir de
        // QUALQUER data já vista no passado, não só da primeira ativação.
        if (hoje > persistido.MaiorDataJaVista)
            SalvarEstadoPersistido(persistido with { MaiorDataJaVista = hoje });

        return hoje > info.ValidoAte
            ? new EstadoDaLicenca(SituacaoDaLicenca.Expirada, info.ValidoAte, info.Tipo, info.ClienteIdHash)
            : new EstadoDaLicenca(SituacaoDaLicenca.Valida, info.ValidoAte, info.Tipo, info.ClienteIdHash);
    }

    public ResultadoDaAtivacao Ativar(string codigoDigitado)
    {
        var codigo = codigoDigitado.Trim();
        var info = CodificadorDeLicenca.Verificar(codigo, ObterChavePublica());

        if (info is null)
            return new ResultadoDaAtivacao(false, "Código inválido — confira se copiou certinho, sem faltar nem sobrar caractere.", null);

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        if (hoje > info.ValidoAte)
            return new ResultadoDaAtivacao(false, $"Este código venceu em {info.ValidoAte:dd/MM/yyyy}. Peça um código novo.", null);

        var maiorDataJaVista = LerEstadoPersistido()?.MaiorDataJaVista is { } anterior && anterior > hoje ? anterior : hoje;
        SalvarEstadoPersistido(new EstadoPersistido(codigo, maiorDataJaVista));

        var situacao = hoje < maiorDataJaVista ? SituacaoDaLicenca.RelogioSuspeito : SituacaoDaLicenca.Valida;
        return new ResultadoDaAtivacao(true, $"Licença ativada — válida até {info.ValidoAte:dd/MM/yyyy}.", new EstadoDaLicenca(situacao, info.ValidoAte, info.Tipo, info.ClienteIdHash));
    }

    private static ECDsa ObterChavePublica()
    {
        var chave = ECDsa.Create();
        chave.ImportSubjectPublicKeyInfo(Convert.FromBase64String(ChavePublicaBase64), out _);
        return chave;
    }

    private EstadoPersistido? LerEstadoPersistido()
    {
        if (!File.Exists(_arquivoDeEstado)) return null;

        try
        {
            var protegido = File.ReadAllBytes(_arquivoDeEstado);
            var json = ProtectedData.Unprotect(protegido, Entropia, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<EstadoPersistido>(json);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or IOException)
        {
            // Arquivo corrompido, de outra máquina/usuário (DPAPI não descriptografa fora de
            // onde foi criado) ou adulterado à mão — trata como "nunca ativou", nunca crasha o
            // app por causa disso (pior caso: pede o código de novo).
            return null;
        }
    }

    private void SalvarEstadoPersistido(EstadoPersistido estado)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivoDeEstado)!);
        var json = JsonSerializer.SerializeToUtf8Bytes(estado);
        var protegido = ProtectedData.Protect(json, Entropia, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_arquivoDeEstado, protegido);
    }
}
