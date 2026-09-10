using FluentAssertions;
using OptimizePro.Core.Impressoras;
using OptimizePro.Data.Entidades;
using OptimizePro.Services.Impressoras.Historico;

namespace OptimizePro.Services.Tests.Impressoras;

public class LeitorCsvHistoricoTests
{
    [Fact]
    public async Task LerIntervalo_LinhaSimples_ConverteMedidasDePolegadaParaMetroECalculaDuracao()
    {
        var pasta = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var csv = Path.Combine(pasta, "History.csv");
            await File.WriteAllLinesAsync(csv, [
                "Start,End,Cost,FileName,IsClipOrTile,WidthInch,HeightInch",
                "1/15/2026 10:00:00 AM,1/15/2026 10:05:00 AM,300,arte.prt,false,40,60",
            ]);

            var maquina = new Maquina { Id = "imp01", Nome = "Impressora 01", Tipo = TipoDeMaquina.Csv, CaminhoHistorico = csv };
            var leitor = new LeitorCsvHistorico();

            var registros = await leitor.LerIntervaloAsync(maquina, "2026-01-01", "2026-01-31");

            registros.Should().ContainSingle();
            var r = registros[0];
            r.MaquinaId.Should().Be("imp01");
            r.Data.Should().Be("2026-01-15");
            r.Tarefa.Should().Be("arte.prt");
            r.ComprimentoDeImpressao.Should().BeApproximately(60 / 39.37, 0.0001);
            r.TempoSegundos.Should().Be(300);
            r.Status.Should().Be("Concluído");
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public async Task LerIntervalo_ForaDoIntervaloDeDatas_NaoRetornaORegistro()
    {
        var pasta = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var csv = Path.Combine(pasta, "History.csv");
            await File.WriteAllLinesAsync(csv, [
                "Start,End,Cost,FileName,IsClipOrTile,WidthInch,HeightInch",
                "1/15/2026 10:00:00 AM,1/15/2026 10:05:00 AM,300,arte.prt,false,40,60",
            ]);

            var maquina = new Maquina { Id = "imp01", Nome = "Impressora 01", Tipo = TipoDeMaquina.Csv, CaminhoHistorico = csv };
            var leitor = new LeitorCsvHistorico();

            var registros = await leitor.LerIntervaloAsync(maquina, "2026-02-01", "2026-02-28");

            registros.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public async Task LerIntervalo_CopiasRepetemOMesmoStart_SegundaCopiaComecaNoFimDaAnterior()
    {
        var pasta = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var csv = Path.Combine(pasta, "History.csv");
            // O PrinterManager repete o Start da tiragem inteira; a 2ª cópia deve usar o End
            // da 1ª como início efetivo, não o Start bruto igual às duas linhas.
            await File.WriteAllLinesAsync(csv, [
                "Start,End,Cost,FileName,IsClipOrTile,WidthInch,HeightInch",
                "1/15/2026 10:00:00 AM,1/15/2026 10:05:00 AM,300,arte.prt,false,40,60",
                "1/15/2026 10:00:00 AM,1/15/2026 10:10:00 AM,300,arte.prt,false,40,60",
            ]);

            var maquina = new Maquina { Id = "imp01", Nome = "Impressora 01", Tipo = TipoDeMaquina.Csv, CaminhoHistorico = csv };
            var leitor = new LeitorCsvHistorico();

            var registros = await leitor.LerIntervaloAsync(maquina, "2026-01-01", "2026-01-31");

            registros.Should().HaveCount(2);
            registros[1].Hora.Should().Be("10:05:00", "o início efetivo da 2ª cópia é o fim da 1ª");
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }
}
