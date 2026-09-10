using System.Text;
using OptimizePro.Data.Entidades;

namespace OptimizePro.Services.Impressoras.Historico;

/// <summary>
/// Porte de <c>impressoras/sources/atBinary.js</c> (impressoras 06/07, §22.4) — registros de
/// tamanho fixo (392 bytes) ordenados por data, lidos por busca binária no arquivo pra não
/// precisar carregar o histórico inteiro pra pegar só um intervalo de dias.
/// </summary>
public sealed class LeitorAtBinarioHistorico : ILeitorDeHistorico
{
    private const int TamanhoDoRegistro = 392;
    private const double RawPorMlDeTinta = 192622951.14307776;
    private const double ToleranciaDeConclusao = 0.001;

    static LeitorAtBinarioHistorico()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<List<RegistroDeImpressao>> LerIntervaloAsync(Maquina maquina, string dataInicioIso, string dataFimIso, CancellationToken ct = default)
    {
        if (maquina.CaminhoHistorico is null) return [];

        await using var fluxo = new FileStream(maquina.CaminhoHistorico, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var tamanho = fluxo.Length;
        if (tamanho % TamanhoDoRegistro != 0)
            throw new InvalidDataException($"PrintHistory inválido: {tamanho} bytes");

        var quantidade = tamanho / TamanhoDoRegistro;
        var indiceInicio = await LimiteInferiorAsync(fluxo, quantidade, dataInicioIso, ct);
        var indiceFim = await LimiteInferiorAsync(fluxo, quantidade, DatasHistorico.SomarDias(dataFimIso, 1), ct);
        var qtd = Math.Max(0, indiceFim - indiceInicio);
        if (qtd == 0) return [];

        var buffer = new byte[qtd * TamanhoDoRegistro];
        fluxo.Seek(indiceInicio * TamanhoDoRegistro, SeekOrigin.Begin);
        await fluxo.ReadExactlyAsync(buffer, ct);

        var resultado = new List<RegistroDeImpressao>();
        for (var i = 0; i < qtd; i++)
        {
            var registro = AnalisarRegistro(buffer.AsSpan(i * TamanhoDoRegistro, TamanhoDoRegistro), maquina, indiceInicio + i);
            if (string.Compare(registro.Data, dataInicioIso, StringComparison.Ordinal) >= 0 &&
                string.Compare(registro.Data, dataFimIso, StringComparison.Ordinal) <= 0)
                resultado.Add(registro);
        }

        return resultado;
    }

    public Task<string> AssinaturaAsync(Maquina maquina, CancellationToken ct = default)
    {
        if (maquina.CaminhoHistorico is null) return Task.FromResult("");
        var info = new FileInfo(maquina.CaminhoHistorico);
        return Task.FromResult($"{info.Length}:{info.LastWriteTimeUtc.Ticks}");
    }

    private static async Task<long> LimiteInferiorAsync(FileStream fluxo, long quantidade, string alvo, CancellationToken ct)
    {
        long lo = 0, hi = quantidade;
        var buffer = new byte[20];
        while (lo < hi)
        {
            var meio = (lo + hi) / 2;
            fluxo.Seek(meio * TamanhoDoRegistro, SeekOrigin.Begin);
            await fluxo.ReadExactlyAsync(buffer, ct);
            var textoData = TextoDeCampo(buffer, 0, 20, Encoding.GetEncoding(1252));
            var data = textoData[..Math.Min(10, textoData.Length)];
            if (string.Compare(data, alvo, StringComparison.Ordinal) < 0) lo = meio + 1;
            else hi = meio;
        }
        return lo;
    }

    private static RegistroDeImpressao AnalisarRegistro(ReadOnlySpan<byte> buffer, Maquina maquina, long indice)
    {
        var win1252 = Encoding.GetEncoding(1252);
        var dataHora = TextoDeCampo(buffer, 0, 20, win1252);
        var tarefa = TextoDeCampo(buffer, 20, 275, win1252);
        var cancelar = (char)(buffer.Length > 275 ? buffer[275] : 0);
        var passada = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(buffer[276..280]);
        var area = BitConverter.ToDouble(buffer[280..288]);
        var concluido = BitConverter.ToDouble(buffer[288..296]);
        var comprimento = BitConverter.ToDouble(buffer[296..304]);
        var tintaRaw = BitConverter.ToDouble(buffer[312..320]);
        var total = BitConverter.ToDouble(buffer[328..336]);
        var horasDecorridas = BitConverter.ToDouble(buffer[336..344]);
        var tintaMl = double.IsFinite(tintaRaw) && tintaRaw >= 0 ? tintaRaw / RawPorMlDeTinta : 0;
        var cancelada = cancelar == 'Y';
        var (percentual, estado) = CalcularProgresso(concluido, total, cancelada);

        return new RegistroDeImpressao
        {
            Id = $"{maquina.Id}|record|{indice}",
            MaquinaId = maquina.Id,
            NomeDaMaquina = maquina.Nome,
            TipoDeOrigem = "at-binary",
            DataHora = dataHora,
            Data = dataHora.Length >= 10 ? dataHora[..10] : dataHora,
            Hora = dataHora.Length >= 19 ? dataHora[11..19] : "",
            Tarefa = tarefa,
            Passada = unchecked((int)passada),
            Status = cancelada ? "Cancelado" : estado == "completed" ? "Concluído" : "Em impressão",
            Cancelada = cancelada,
            ComErro = false,
            AreaDeImpressao = area,
            ComprimentoDeImpressao = comprimento,
            Concluido = concluido,
            Total = total,
            PercentualDeProgresso = percentual,
            EstadoDoProgresso = estado,
            HorasDecorridas = horasDecorridas,
            TempoSegundos = Math.Max(0, (int)Math.Round(horasDecorridas * 3600)),
            TintaMl = tintaMl,
            TintaExperimental = true,
            ReferenciaDePreview = tarefa,
        };
    }

    private static (double Percentual, string Estado) CalcularProgresso(double concluido, double total, bool cancelada)
    {
        if (cancelada)
            return (total > 0 ? Math.Clamp(concluido / total * 100, 0, 100) : 0, "cancelled");

        if (!(total > 0)) return (0, "unknown");

        var pct = Math.Clamp(concluido / total * 100, 0, 100);
        var completo = Math.Abs(concluido - total) <= ToleranciaDeConclusao || concluido >= total;
        return (completo ? 100 : pct, completo ? "completed" : "printing");
    }

    private static string TextoDeCampo(ReadOnlySpan<byte> buffer, int inicio, int fim, Encoding codificacao)
    {
        var fatia = buffer[inicio..Math.Min(fim, buffer.Length)];
        var indiceZero = fatia.IndexOf((byte)0);
        if (indiceZero >= 0) fatia = fatia[..indiceZero];
        return codificacao.GetString(fatia).Trim();
    }
}
