using FluentAssertions;

namespace OptimizePro.Painel.Tests;

public class HashDeSenhaTests
{
    [Fact]
    public void GerarEConferir_SenhaCorreta_Confere()
    {
        var (hash, sal) = HashDeSenha.Gerar("minhaSenha123");
        HashDeSenha.Conferir("minhaSenha123", hash, sal).Should().BeTrue();
    }

    [Fact]
    public void Conferir_SenhaErrada_NaoConfere()
    {
        var (hash, sal) = HashDeSenha.Gerar("minhaSenha123");
        HashDeSenha.Conferir("outraSenha", hash, sal).Should().BeFalse();
    }

    [Fact]
    public void Gerar_MesmaSenhaDuasVezes_SaisDiferentesEHashesDiferentes()
    {
        var (hash1, sal1) = HashDeSenha.Gerar("minhaSenha123");
        var (hash2, sal2) = HashDeSenha.Gerar("minhaSenha123");

        sal1.Should().NotEqual(sal2, "o sal deve ser aleatório a cada geração");
        hash1.Should().NotEqual(hash2);
    }

    [Fact]
    public void NuncaGuardaASenhaEmTextoPuroDentroDoHashOuDoSal()
    {
        var (hash, sal) = HashDeSenha.Gerar("segredo");
        var senhaBytes = System.Text.Encoding.UTF8.GetBytes("segredo");

        hash.Should().NotEqual(senhaBytes);
        sal.Should().NotEqual(senhaBytes);
    }
}
