using System;
using Optimize.App.ViewModels;

namespace Optimize.App.Services;

/// <summary>
/// Ponto único de navegação entre telas — qualquer ViewModel pode pedir pra trocar de tela
/// (ex.: Moldes → assistente de criação → volta pra Moldes) sem depender diretamente da
/// <see cref="MainWindowViewModel"/>. Escopada junto com o resto da sessão (§16).
/// </summary>
public interface INavegador
{
    event Action<TipoDeTela, object?>? Navegado;

    void NavegarPara(TipoDeTela tipo, object? parametro = null);
}

public sealed class Navegador : INavegador
{
    public event Action<TipoDeTela, object?>? Navegado;

    public void NavegarPara(TipoDeTela tipo, object? parametro = null) => Navegado?.Invoke(tipo, parametro);
}
