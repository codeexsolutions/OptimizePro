using FluentAssertions;
using OptimizePro.Core.Impressoras;
using OptimizePro.Data.Entidades;
using OptimizePro.Services.Impressoras.Historico;

namespace OptimizePro.Services.Tests.Impressoras;

public class LeitorXmlHistoricoTests
{
    [Fact]
    public async Task LerIntervalo_PastaDoDia_LeOsFloatsEOTempoEmTicks()
    {
        var raiz = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var pastaDoDia = Path.Combine(raiz, "2026", "202601", "20260115");
            Directory.CreateDirectory(pastaDoDia);
            await File.WriteAllTextAsync(Path.Combine(pastaDoDia, "job1.xml"), """
                <PrintRecordList>
                  <PrintRecord>
                    <UIJob>
                      <string>Tarefa1.prt</string>
                      <dateTime>2026-01-15T10:00:00</dateTime>
                      <JobStatus>Completed</JobStatus>
                    </UIJob>
                    <float>2.5</float>
                    <float>3.0</float>
                    <long>36000000000</long>
                  </PrintRecord>
                </PrintRecordList>
                """);

            var maquina = new Maquina { Id = "imp04", Nome = "Impressora 04", Tipo = TipoDeMaquina.Xml, CaminhoHistorico = raiz };
            var leitor = new LeitorXmlHistorico();

            var registros = await leitor.LerIntervaloAsync(maquina, "2026-01-15", "2026-01-15");

            registros.Should().ContainSingle();
            var r = registros[0];
            r.Tarefa.Should().Be("Tarefa1.prt");
            r.ComprimentoDeImpressao.Should().Be(2.5);
            r.AreaDeImpressao.Should().Be(3.0);
            r.TempoSegundos.Should().Be(3600);
            r.Status.Should().Be("Completed");
            r.ComErro.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public async Task LerIntervalo_PastaDoDiaInexistente_RetornaListaVazia()
    {
        var raiz = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var maquina = new Maquina { Id = "imp04", Nome = "Impressora 04", Tipo = TipoDeMaquina.Xml, CaminhoHistorico = raiz };
            var leitor = new LeitorXmlHistorico();

            var registros = await leitor.LerIntervaloAsync(maquina, "2026-01-15", "2026-01-15");

            registros.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }
}
