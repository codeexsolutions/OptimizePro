using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Optimize.App.Services;
using OptimizePro.Painel;

namespace Optimize.App.ViewModels;

/// <summary>
/// Tela de login do gate de módulos (§25) — só aparece quando já existe pelo menos um usuário
/// do painel cacheado localmente (ver <c>App.axaml.cs</c>); confere login+senha contra esse
/// cache OFFLINE (mesmo <see cref="IAutenticacaoService"/> do painel, §23.1), sem depender de
/// rede pra alguém no chão de fábrica conseguir entrar.
/// </summary>
public sealed partial class LoginDoPainelViewModel(IAutenticacaoService autenticacao, SessaoDoPainel sessao) : ViewModelBase
{
    [ObservableProperty]
    public partial string Login { get; set; } = "";

    [ObservableProperty]
    public partial string Senha { get; set; } = "";

    [ObservableProperty]
    public partial string? Erro { get; set; }

    [ObservableProperty]
    public partial bool Autenticado { get; set; }

    [RelayCommand]
    private async Task Entrar()
    {
        Erro = null;

        if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Senha))
        {
            Erro = "Informe login e senha.";
            return;
        }

        var resultado = await autenticacao.AutenticarAsync(Login, Senha);
        if (!resultado.Sucesso || resultado.Usuario is null)
        {
            Erro = resultado.Erro;
            return;
        }

        sessao.UsuarioAtual = resultado.Usuario;
        Autenticado = true;
    }
}
