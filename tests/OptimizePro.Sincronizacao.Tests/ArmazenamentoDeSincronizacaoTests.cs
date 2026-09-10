using FluentAssertions;
using OptimizePro.Sincronizacao;

namespace OptimizePro.Sincronizacao.Tests;

public class ArmazenamentoDeSincronizacaoTests
{
    [Fact]
    public void Ler_ArquivoInexistente_RetornaNulo()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"sinc-teste-{Guid.NewGuid():N}.dat");
        var armazenamento = new ArmazenamentoDeSincronizacao(caminho);

        armazenamento.Ler().Should().BeNull();
    }

    [Fact]
    public void SalvarELer_RoundTripPreservaOsValores()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"sinc-teste-{Guid.NewGuid():N}.dat");
        try
        {
            var armazenamento = new ArmazenamentoDeSincronizacao(caminho);
            var estado = new EstadoLocalDeSincronizacao("instalacao-123", "chave-abc");

            armazenamento.Salvar(estado);
            var lido = armazenamento.Ler();

            lido.Should().Be(estado);
        }
        finally
        {
            if (File.Exists(caminho)) File.Delete(caminho);
        }
    }

    [Fact]
    public void Ler_ArquivoCorrompido_RetornaNuloEmVezDeLancar()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"sinc-teste-{Guid.NewGuid():N}.dat");
        try
        {
            File.WriteAllBytes(caminho, [1, 2, 3, 4, 5]); // bytes aleatórios, não é um blob DPAPI válido
            var armazenamento = new ArmazenamentoDeSincronizacao(caminho);

            armazenamento.Ler().Should().BeNull();
        }
        finally
        {
            if (File.Exists(caminho)) File.Delete(caminho);
        }
    }
}
