using FluentAssertions;
using OptimizePro.Core.Encaixe;

namespace OptimizePro.Core.Tests.Encaixe;

public class EncaixadorPorVaosTests
{
    [Fact]
    public void MelhorVaga_TecidoVazio_EncostaNoComecoDoRolo()
    {
        var tecido = new TecidoPorVaos(colsTecido: 3);
        var forma = new Forma(1, [0], [1], []); // 1 coluna, 2 linhas de altura

        var resultado = EncaixadorPorVaos.MelhorVaga(tecido, forma);

        resultado.Should().NotBeNull();
        resultado!.Value.Y.Should().Be(0);
        resultado.Value.P1.Should().Be(2); // fundo
    }

    [Fact]
    public void MelhorVaga_SemVaoNenhum_ComportaComoORelevoSimples()
    {
        var tecido = new TecidoPorVaos(colsTecido: 1); // só 1 coluna — obriga a empilhar
        var base1 = new Forma(1, [0], [0], []); // 1x1
        var pos1 = EncaixadorPorVaos.MelhorVaga(tecido, base1)!.Value;
        EncaixadorPorVaos.Ocupar(tecido, base1, pos1.X, pos1.Y, donoId: 0);

        // Segunda peça igual — sem vão nenhum, tem que empilhar em cima (y=1), não achar buraco.
        var pos2 = EncaixadorPorVaos.MelhorVaga(tecido, base1)!.Value;

        pos2.Y.Should().Be(1);
    }

    /// <summary>
    /// O caso que só o encaixe por vãos resolve: uma peça já assentada deixa uma coluna com
    /// um segmento "flutuando" bem acima (ex.: o braço de um formato em L/gancho), sobrando
    /// espaço livre ABAIXO dele naquela coluna. Um motor por relevo simples (uma altura só por
    /// coluna) nunca enxergaria esse espaço — pra ele a coluna já estaria ocupada até o topo do
    /// segmento. Aqui uma peça pequena tem que descer e PARAR DENTRO desse vão.
    /// </summary>
    [Fact]
    public void MelhorVaga_ComVaoAbertoAbaixoDeUmSegmentoFlutuante_EncontraOBuraco()
    {
        var tecido = new TecidoPorVaos(colsTecido: 2);

        // "Ponte": coluna 0 encosta em y=0 (topo=0,base=0), coluna 1 só tem um segmento lá em
        // cima, em y=8 (topo=8,base=8) — nada ocupa a coluna 1 entre as linhas 0 e 7.
        var ponte = new Forma(2, [0, 8], [0, 8], []);
        EncaixadorPorVaos.Ocupar(tecido, ponte, x: 0, y: 0, donoId: 0);

        // Pelo relevo simples, a coluna 1 pareceria ocupada até a linha 9 (Perfil[1]=9). Pelos
        // intervalos, ela só tem UM trecho ocupado (linha 8) — o vão de baixo (linhas 0-7) está
        // livre e "fechado" (nada mais abaixo dele na lista, mas o cálculo de maiorVao já cobre
        // esse caso: é o espaço entre o início do rolo e o primeiro intervalo).
        var pequena = new Forma(1, [0], [0], []); // 1x1

        var resultado = EncaixadorPorVaos.MelhorVaga(tecido, pequena)!.Value;

        resultado.X.Should().Be(1, "só a coluna 1 tem vão livre embaixo do segmento flutuante");
        resultado.Y.Should().Be(0, "a peça deve descer e parar dentro do vão, não ir para y=9");
        resultado.P1.Should().Be(1); // fundo — bem menor que os 10 que o relevo simples exigiria
    }

    [Fact]
    public void Ocupar_DuasPecasNaMesmaColuna_NaoDeixaSobrepor()
    {
        var tecido = new TecidoPorVaos(colsTecido: 1);
        var forma = new Forma(1, [0], [2], []); // altura 3

        var pos1 = EncaixadorPorVaos.MelhorVaga(tecido, forma)!.Value;
        EncaixadorPorVaos.Ocupar(tecido, forma, pos1.X, pos1.Y, donoId: 0);

        var pos2 = EncaixadorPorVaos.MelhorVaga(tecido, forma)!.Value;

        // Não pode sobrepor: o intervalo da segunda tem que começar depois do fim da primeira.
        (pos2.Y).Should().BeGreaterThanOrEqualTo(pos1.Y + 3);
    }

    [Fact]
    public void MelhorVaga_FormaMaiorQueOTecido_DevolveNulo()
    {
        var tecido = new TecidoPorVaos(colsTecido: 2);
        var forma = new Forma(3, [0, 0, 0], [0, 0, 0], []);

        EncaixadorPorVaos.MelhorVaga(tecido, forma).Should().BeNull();
    }
}
