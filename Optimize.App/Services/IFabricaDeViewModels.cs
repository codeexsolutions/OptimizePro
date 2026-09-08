using Optimize.App.ViewModels;

namespace Optimize.App.Services;

/// <summary>
/// Resolve o ViewModel de cada tela via DI (Microsoft.Extensions.DependencyInjection),
/// para que o MainWindowViewModel não precise conhecer construtores/serviços concretos.
/// </summary>
public interface IFabricaDeViewModels
{
    ViewModelBase Criar(TipoDeTela tipo, object? parametro = null);
}

/// <summary>ViewModels que recebem um parâmetro de navegação (ex.: id do projeto a editar) implementam isto — a fábrica chama <see cref="Receber"/> logo após resolver a instância.</summary>
public interface IRecebeParametro
{
    void Receber(object? parametro);
}
