using FluentAssertions;
using OptimizePro.Core.Encaixe.Busca;

namespace OptimizePro.Core.Tests.Encaixe.Busca;

public class EmbaralhamentoTests
{
    [Fact]
    public void Forte_ProduzUmaPermutacaoValida()
    {
        var ordem = Embaralhamento.Forte(10, new Random(42));

        ordem.Should().BeEquivalentTo(Enumerable.Range(0, 10));
    }

    [Fact]
    public void Forte_MesmaSeed_ProduzOMesmoResultado()
    {
        var a = Embaralhamento.Forte(20, new Random(123));
        var b = Embaralhamento.Forte(20, new Random(123));

        a.Should().Equal(b);
    }

    [Fact]
    public void Leve_ProduzUmaPermutacaoValidaDaOrdemBase()
    {
        int[] ordemBase = [4, 1, 3, 0, 2];

        var ordem = Embaralhamento.Leve(ordemBase, new Random(7));

        ordem.Should().BeEquivalentTo(ordemBase);
    }

    [Fact]
    public void Leve_ComMenosDeDoisElementos_DevolveIgual()
    {
        int[] ordemBase = [0];

        Embaralhamento.Leve(ordemBase, new Random(1)).Should().Equal(ordemBase);
    }

    [Fact]
    public void Leve_PerturbaMenosQueForte_NaMaioriaDasVezesPreservaMaisPosicoes()
    {
        var ordemBase = Enumerable.Range(0, 100).ToArray();
        var aleatorio = new Random(99);

        var leve = Embaralhamento.Leve(ordemBase, aleatorio);
        var forte = Embaralhamento.Forte(100, aleatorio);

        var posicoesIguaisLeve = ordemBase.Where((v, i) => leve[i] == v).Count();
        var posicoesIguaisForte = ordemBase.Where((v, i) => forte[i] == v).Count();

        posicoesIguaisLeve.Should().BeGreaterThan(posicoesIguaisForte);
    }

    [Fact]
    public void ReconstruirRabo_ProduzUmaPermutacaoValidaDaOrdemBase()
    {
        var ordemBase = Enumerable.Range(0, 20).ToArray();

        var ordem = Embaralhamento.ReconstruirRabo(ordemBase, new Random(7));

        ordem.Should().BeEquivalentTo(ordemBase);
    }

    [Fact]
    public void ReconstruirRabo_ComMenosDeDoisElementos_DevolveIgual()
    {
        int[] ordemBase = [0];

        Embaralhamento.ReconstruirRabo(ordemBase, new Random(1)).Should().Equal(ordemBase);
    }

    [Fact]
    public void ReconstruirRabo_NuncaMudaACabeca_SoOTrechoFinal()
    {
        var ordemBase = Enumerable.Range(0, 50).ToArray();
        var aleatorio = new Random(2024);

        var ordem = Embaralhamento.ReconstruirRabo(ordemBase, aleatorio, fracaoDoRabo: 0.2);

        // Rabo = 20% de 50 = 10 últimas posições — as 40 primeiras (a "cabeça") não podem mudar.
        ordem.Take(40).Should().Equal(ordemBase.Take(40));
    }

    [Fact]
    public void ReconstruirRabo_ComFracaoMaior_MexeEmMaisPosicoesDoFinal()
    {
        var ordemBase = Enumerable.Range(0, 200).ToArray();
        var aleatorio = new Random(55);

        var ordem = Embaralhamento.ReconstruirRabo(ordemBase, aleatorio, fracaoDoRabo: 0.5);

        // Rabo = 50% — as 100 primeiras posições continuam intactas (a fração NÃO é o rabo).
        ordem.Take(100).Should().Equal(ordemBase.Take(100));
    }

    [Fact]
    public void RepararPior_ProduzUmaPermutacaoValidaDaOrdemBase()
    {
        int[] ordemBase = [0, 1, 2, 3, 4, 5, 6, 7];

        var ordem = Embaralhamento.RepararPior(ordemBase, itensDaPiorUnidade: [5, 6], new Random(1));

        ordem.Should().BeEquivalentTo(ordemBase);
    }

    [Fact]
    public void RepararPior_MoveABlocoDaPiorUnidadeParaAntesDaPosicaoOriginal()
    {
        // "5,6" formavam uma unidade (ex.: dupla) na posição 5-6 da ordem — reparar tem que
        // devolver os dois JUNTOS (preservando a ordem relativa: 5 antes de 6) em algum ponto
        // ANTES de onde estavam, nunca depois.
        int[] ordemBase = [0, 1, 2, 3, 4, 5, 6, 7];

        for (var seed = 0; seed < 20; seed++)
        {
            var ordem = Embaralhamento.RepararPior(ordemBase, itensDaPiorUnidade: [5, 6], new Random(seed));

            var indice5 = Array.IndexOf(ordem, 5);
            var indice6 = Array.IndexOf(ordem, 6);

            indice6.Should().Be(indice5 + 1, "5 e 6 formavam uma unidade só, têm que continuar juntos e na mesma ordem relativa");
            indice5.Should().BeLessThanOrEqualTo(5, "a unidade só pode ir pra uma posição IGUAL ou MAIS CEDO que a original, nunca mais tarde");
        }
    }

    [Fact]
    public void RepararPior_UnidadeDeUmItemSoJaNoComeco_NaoMuda()
    {
        int[] ordemBase = [0, 1, 2, 3];

        var ordem = Embaralhamento.RepararPior(ordemBase, itensDaPiorUnidade: [0], new Random(1));

        ordem.Should().Equal(ordemBase);
    }

    [Fact]
    public void RepararPior_SemItensDaPiorUnidade_DevolveCopiaIgual()
    {
        int[] ordemBase = [0, 1, 2, 3];

        Embaralhamento.RepararPior(ordemBase, itensDaPiorUnidade: [], new Random(1)).Should().Equal(ordemBase);
    }

    [Fact]
    public void RepararPior_ItensQueNaoExistemNaOrdem_DevolveCopiaIgual()
    {
        int[] ordemBase = [0, 1, 2, 3];

        Embaralhamento.RepararPior(ordemBase, itensDaPiorUnidade: [99], new Random(1)).Should().Equal(ordemBase);
    }
}
