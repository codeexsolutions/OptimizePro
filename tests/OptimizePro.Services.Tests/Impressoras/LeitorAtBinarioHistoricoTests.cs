using System.Text;
using FluentAssertions;
using OptimizePro.Core.Impressoras;
using OptimizePro.Data.Entidades;
using OptimizePro.Services.Impressoras.Historico;

namespace OptimizePro.Services.Tests.Impressoras;

public class LeitorAtBinarioHistoricoTests
{
    private const int TamanhoDoRegistro = 392;

    static LeitorAtBinarioHistoricoTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static byte[] MontarRegistro(string dataHora, string tarefa, char cancelar, uint passada,
        double area, double concluido, double comprimento, double inkRaw, double total, double horasDecorridas)
    {
        var buffer = new byte[TamanhoDoRegistro];
        var win1252 = Encoding.GetEncoding(1252);

        win1252.GetBytes(dataHora).CopyTo(buffer, 0);
        win1252.GetBytes(tarefa).CopyTo(buffer, 20);
        buffer[275] = (byte)cancelar;
        BitConverter.GetBytes(passada).CopyTo(buffer, 276);
        BitConverter.GetBytes(area).CopyTo(buffer, 280);
        BitConverter.GetBytes(concluido).CopyTo(buffer, 288);
        BitConverter.GetBytes(comprimento).CopyTo(buffer, 296);
        BitConverter.GetBytes(inkRaw).CopyTo(buffer, 312);
        BitConverter.GetBytes(total).CopyTo(buffer, 328);
        BitConverter.GetBytes(horasDecorridas).CopyTo(buffer, 336);
        return buffer;
    }

    [Fact]
    public async Task LerIntervalo_RegistroConcluido_LeCamposEMarcaProgressoCompleto()
    {
        var arquivo = Path.GetTempFileName();
        try
        {
            var registro = MontarRegistro("2026-01-15 10:00:00", "Tarefa1.prt", 'N', 1,
                area: 3.0, concluido: 100, comprimento: 2.5, inkRaw: 192622951.14307776 * 5, total: 100, horasDecorridas: 1.0);
            await File.WriteAllBytesAsync(arquivo, registro);

            var maquina = new Maquina { Id = "imp06", Nome = "Impressora 06", Tipo = TipoDeMaquina.AtBinario, CaminhoHistorico = arquivo };
            var leitor = new LeitorAtBinarioHistorico();

            var registros = await leitor.LerIntervaloAsync(maquina, "2026-01-01", "2026-01-31");

            registros.Should().ContainSingle();
            var r = registros[0];
            r.Data.Should().Be("2026-01-15");
            r.Hora.Should().Be("10:00:00");
            r.Tarefa.Should().Be("Tarefa1.prt");
            r.Cancelada.Should().BeFalse();
            r.EstadoDoProgresso.Should().Be("completed");
            r.PercentualDeProgresso.Should().Be(100);
            r.ComprimentoDeImpressao.Should().Be(2.5);
            r.TintaMl.Should().BeApproximately(5, 0.0001);
            r.Status.Should().Be("Concluído");
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    [Fact]
    public async Task LerIntervalo_RegistroCancelado_MarcaStatusCanceladoENaoConcluido()
    {
        var arquivo = Path.GetTempFileName();
        try
        {
            var registro = MontarRegistro("2026-01-15 10:00:00", "Tarefa1.prt", 'Y', 1,
                area: 1.0, concluido: 30, comprimento: 1.0, inkRaw: 0, total: 100, horasDecorridas: 0.2);
            await File.WriteAllBytesAsync(arquivo, registro);

            var maquina = new Maquina { Id = "imp06", Nome = "Impressora 06", Tipo = TipoDeMaquina.AtBinario, CaminhoHistorico = arquivo };
            var leitor = new LeitorAtBinarioHistorico();

            var registros = await leitor.LerIntervaloAsync(maquina, "2026-01-01", "2026-01-31");

            registros.Should().ContainSingle();
            registros[0].Cancelada.Should().BeTrue();
            registros[0].Status.Should().Be("Cancelado");
            registros[0].EstadoDoProgresso.Should().Be("cancelled");
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    [Fact]
    public async Task LerIntervalo_ForaDoIntervaloDeDatas_RetornaVazio()
    {
        var arquivo = Path.GetTempFileName();
        try
        {
            var registro = MontarRegistro("2026-01-15 10:00:00", "Tarefa1.prt", 'N', 1,
                area: 1.0, concluido: 100, comprimento: 1.0, inkRaw: 0, total: 100, horasDecorridas: 0.2);
            await File.WriteAllBytesAsync(arquivo, registro);

            var maquina = new Maquina { Id = "imp06", Nome = "Impressora 06", Tipo = TipoDeMaquina.AtBinario, CaminhoHistorico = arquivo };
            var leitor = new LeitorAtBinarioHistorico();

            var registros = await leitor.LerIntervaloAsync(maquina, "2026-02-01", "2026-02-28");

            registros.Should().BeEmpty();
        }
        finally
        {
            File.Delete(arquivo);
        }
    }
}
