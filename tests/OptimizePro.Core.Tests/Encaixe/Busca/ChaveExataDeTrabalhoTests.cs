using FluentAssertions;
using OptimizePro.Core.Encaixe;
using OptimizePro.Core.Encaixe.Busca;

namespace OptimizePro.Core.Tests.Encaixe.Busca;

public class ChaveExataDeTrabalhoTests
{
    private static ItemParaChaveExata Item(string id, int qtd = 1, TipoDeGiro giro = TipoDeGiro.MantemSentido) =>
        new(id, [new PontoXY(0, 0), new PontoXY(10, 0), new PontoXY(10, 20), new PontoXY(0, 20)], qtd, giro);

    [Fact]
    public void Calcular_MesmosItensNaMesmaConfiguracao_DevolveAMesmaChave()
    {
        var itens = new[] { Item("a"), Item("b", 2) };

        var c1 = ChaveExataDeTrabalho.Calcular(itens, 150, 0.5, 1);
        var c2 = ChaveExataDeTrabalho.Calcular(itens, 150, 0.5, 1);

        c1.Should().Be(c2);
    }

    [Fact]
    public void Calcular_OrdemDosItensNaoImporta()
    {
        var c1 = ChaveExataDeTrabalho.Calcular([Item("a"), Item("b")], 150, 0.5, 1);
        var c2 = ChaveExataDeTrabalho.Calcular([Item("b"), Item("a")], 150, 0.5, 1);

        c1.Should().Be(c2);
    }

    [Fact]
    public void Calcular_QuantidadeDiferente_DevolveChaveDiferente()
    {
        var c1 = ChaveExataDeTrabalho.Calcular([Item("a", 1)], 150, 0.5, 1);
        var c2 = ChaveExataDeTrabalho.Calcular([Item("a", 2)], 150, 0.5, 1);

        c1.Should().NotBe(c2);
    }

    [Fact]
    public void Calcular_GiroDiferente_DevolveChaveDiferente()
    {
        var c1 = ChaveExataDeTrabalho.Calcular([Item("a", giro: TipoDeGiro.MantemSentido)], 150, 0.5, 1);
        var c2 = ChaveExataDeTrabalho.Calcular([Item("a", giro: TipoDeGiro.Livre)], 150, 0.5, 1);

        c1.Should().NotBe(c2);
    }

    [Fact]
    public void Calcular_LarguraDoTecidoDiferente_DevolveChaveDiferente()
    {
        var itens = new[] { Item("a") };

        var c1 = ChaveExataDeTrabalho.Calcular(itens, 150, 0.5, 1);
        var c2 = ChaveExataDeTrabalho.Calcular(itens, 160, 0.5, 1);

        c1.Should().NotBe(c2);
    }
}
