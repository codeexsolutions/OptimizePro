using OptimizePro.Painel;

namespace Optimize.App.Services;

/// <summary>
/// Quem está logado no gate de módulos do desktop (§25 — login no Optimize.App espelhando os
/// módulos liberados no painel remoto). Singleton pra vida inteira do processo — não é uma
/// sessão web com expiração, é só "quem apertou o botão Entrar nesta instância aberta".
/// <see cref="UsuarioAtual"/> null significa duas coisas possíveis: ninguém logou ainda, OU o
/// gate está desativado porque não existe nenhum usuário cacheado localmente (instalação que
/// nunca configurou usuários no painel — ver <c>App.axaml.cs</c>).
/// </summary>
public sealed class SessaoDoPainel
{
    public Usuario? UsuarioAtual { get; set; }
}
