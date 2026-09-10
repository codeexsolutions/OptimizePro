using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Optimize.App.Services;
using Optimize.App.ViewModels;
using Optimize.App.Views;
using OptimizePro.Core.Moldes;
using OptimizePro.Core.Moldes.Dxf;
using OptimizePro.Core.Moldes.Pdf;
using OptimizePro.Core.Moldes.Plt;
using OptimizePro.Core.Moldes.Svg;
using OptimizePro.Data;
using OptimizePro.Data.Repositorios;
using OptimizePro.Services.Armazenamento;
using OptimizePro.Services.Arquivos;
using OptimizePro.Services.Configuracoes;
using OptimizePro.Services.Encaixe;
using OptimizePro.Services.Licenciamento;
using OptimizePro.Services.Moldes;
using OptimizePro.Services.Impressoras;
using OptimizePro.Services.Impressoras.Historico;
using OptimizePro.Services.Projetos;
using OptimizePro.Services.Vetor;
using OptimizePro.Servidor;
using OptimizePro.Painel;
using OptimizePro.Sincronizacao;

namespace Optimize.App;

public partial class App : Application
{
    private IHost? _host;
    private IServiceScope? _escopoDaSessao;
    private ServidorDoPainel? _servidorDoPainel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var caminhos = new CaminhosDoApp();

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton(caminhos);
                    services.AddSingleton<LicencaService>();
                    services.AddDbContext<OptimizeDbContext>(o => o.UseSqlite($"Data Source={caminhos.BancoDeDados}"));

                    services.AddScoped<IMoldeRepository, MoldeRepository>();
                    services.AddScoped<IProjetoRepository, ProjetoRepository>();
                    services.AddScoped<IEncaixeMemoriaRepository, EncaixeMemoriaRepository>();
                    services.AddScoped<IConfiguracaoRepository, ConfiguracaoRepository>();
                    services.AddSingleton<IArquivoService, ArquivoService>();
                    services.AddScoped<IMoldeService, MoldeService>();
                    services.AddScoped<IProjetoService, ProjetoService>();
                    services.AddScoped<IEncaixeMemoriaService, EncaixeMemoriaService>();
                    services.AddScoped<IEncaixeService, EncaixeService>();
                    services.AddSingleton<IVetorService, VetorService>();
                    services.AddScoped<IConfiguracaoService, ConfiguracaoService>();
                    services.AddScoped<IMaquinaRepository, MaquinaRepository>();
                    services.AddSingleton<VarreduraDeRedeService>();
                    services.AddSingleton<GerenciadorDeVarredura>();
                    services.AddScoped<IMaquinaService, MaquinaService>();
                    services.AddScoped<IRegistroDeImpressaoRepository, RegistroDeImpressaoRepository>();
                    services.AddSingleton<LeitorCsvHistorico>();
                    services.AddSingleton<LeitorXmlHistorico>();
                    services.AddSingleton<LeitorAtBinarioHistorico>();
                    services.AddSingleton<FabricaDeLeitorDeHistorico>();
                    services.AddScoped<SincronizadorDeHistoricoService>();
                    services.AddScoped<IHistoricoService, HistoricoService>();
                    services.AddScoped<IReposicaoService, ReposicaoService>();
                    services.AddScoped<IPedidoRepository, PedidoRepository>();
                    services.AddScoped<IPedidoService, PedidoService>();
                    services.AddSingleton<GeradorDeFolhaDePedido>();
                    services.AddScoped<IOrdemDeServicoRepository, OrdemDeServicoRepository>();
                    services.AddScoped<IOrdemDeServicoService, OrdemDeServicoService>();
                    services.AddSingleton<ClientePainelEmTempoReal>();

                    // Painel do proprietário (§23) — mesmo arquivo dados.db, schema e histórico
                    // de migrations PRÓPRIOS (nunca colidem com o OptimizeDbContext acima).
                    services.AddDbContext<PainelDbContext>(o => o.UseSqlite(
                        $"Data Source={caminhos.BancoDeDados}",
                        x => x.MigrationsHistoryTable("__EFMigrationsHistory_Painel")));

                    // Sincronização com a Central (§24) — best-effort; sem OPTIMIZE_CENTRAL_URL
                    // configurada, ClienteCentralHttp.Configurado fica false e nada é enviado.
                    services.AddSingleton(ConfiguracaoDaCentral.DoAmbiente());
                    services.AddSingleton<IClienteCentralHttp, ClienteCentralHttp>();
                    services.AddSingleton<ArmazenamentoDeSincronizacao>();
                    services.AddScoped<SincronizacaoService>();
                    services.AddHostedService<SincronizadorEmSegundoPlano>();
                    // TODO (backend): registrar aqui IDisparoService (OptimizePro.Services) —
                    // ver docs/ARQUITETURA-DOTNET-DESKTOP-MVVM.md §16.

                    services.AddTransient<ILeitorDeMolde, LeitorDxf>();
                    services.AddTransient<ILeitorDeMolde, LeitorPlt>();
                    services.AddTransient<ILeitorDeMolde, LeitorSvg>();
                    services.AddTransient<ILeitorDeMolde, LeitorPdf>();

                    // Escopo (não singleton): resolvida dentro do escopo único da sessão criado
                    // abaixo, pra o IServiceProvider injetado nela enxergar os mesmos
                    // serviços/DbContext "scoped" (IMoldeService etc.) — resolver um serviço
                    // scoped direto do provider raiz é um erro de DI clássico.
                    services.AddScoped<IFabricaDeViewModels, FabricaDeViewModels>();
                    services.AddScoped<INavegador, Navegador>();

                    services.AddTransient<MainWindowViewModel>();
                    services.AddTransient<MoldesViewModel>();
                    services.AddTransient<MoldeWizardViewModel>();
                    services.AddTransient<MoldeArteEnvioViewModel>();
                    services.AddTransient<ProjetosViewModel>();
                    services.AddTransient<ProjetoEditorViewModel>();
                    services.AddTransient<EncaixeViewModel>();
                    services.AddTransient<VetorViewModel>();
                    services.AddTransient<MaquinasViewModel>();
                    services.AddTransient<HistoricoViewModel>();
                    services.AddTransient<ReposicaoViewModel>();
                    services.AddTransient<PedidosViewModel>();
                    services.AddTransient<OrdensDeServicoViewModel>();
                    services.AddTransient<ImpressorasViewModel>();
                    services.AddTransient<DisparoViewModel>();
                    services.AddTransient<ConfiguracoesViewModel>();
                })
                .Build();

            // Precisa ser iniciado explicitamente pro IHostedService (SincronizadorEmSegundoPlano,
            // §24.2) rodar de verdade — sem isto o host existe só como container de DI, e o
            // ExecuteAsync do BackgroundService nunca dispara.
            _host.Start();

            using (var escopoDeMigracao = _host.Services.CreateScope())
            {
                escopoDeMigracao.ServiceProvider.GetRequiredService<OptimizeDbContext>().Database.Migrate();
                escopoDeMigracao.ServiceProvider.GetRequiredService<PainelDbContext>().Database.Migrate();
            }

            // Um único escopo pra vida inteira da sessão desktop — não há "requisição" aqui
            // como numa API web; ViewModels/Services/DbContext vivem enquanto o app estiver
            // aberto (§16).
            _escopoDaSessao = _host.Services.CreateScope();

            // Trava de acesso (§ "acesso controlado", 02/09/2026): sem licença válida, o app
            // nem chega a abrir a MainWindow — fica preso na tela de ativação até um código bom
            // ser digitado ou o usuário fechar o app.
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnLastWindowClose;

            var licencaService = _host.Services.GetRequiredService<LicencaService>();
            var estadoDaLicenca = licencaService.ObterEstado();

            if (estadoDaLicenca.Liberado)
            {
                desktop.MainWindow = CriarMainWindow();
                IniciarServidorDoPainel(caminhos);
            }
            else
            {
                var licencaViewModel = new LicencaViewModel(licencaService);
                if (estadoDaLicenca.Situacao != SituacaoDaLicenca.NuncaAtivada)
                    licencaViewModel.DefinirMotivoDoBloqueio(estadoDaLicenca.Situacao, estadoDaLicenca.ValidoAte);

                var licencaWindow = new LicencaWindow(licencaViewModel);
                licencaWindow.Closed += (_, _) =>
                {
                    if (!licencaViewModel.Ativado)
                    {
                        desktop.Shutdown();
                        return;
                    }

                    var mainWindow = CriarMainWindow();
                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    IniciarServidorDoPainel(caminhos);
                };

                desktop.MainWindow = licencaWindow;
            }

            desktop.ShutdownRequested += (_, _) =>
            {
                _servidorDoPainel?.PararAsync().GetAwaiter().GetResult();
                _escopoDaSessao?.Dispose();
                _host.StopAsync().GetAwaiter().GetResult();
                _host.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private MainWindow CriarMainWindow() => new()
    {
        DataContext = _escopoDaSessao!.ServiceProvider.GetRequiredService<MainWindowViewModel>(),
    };

    // Só sobe com licença liberada (mesma trava do app inteiro) — o painel expõe dados da
    // fábrica pra rede local, então não faz sentido ligá-lo antes da ativação (§22.2).
    // Falha ao subir (porta ocupada, por exemplo) não deve derrubar o app desktop: só o painel
    // remoto fica indisponível, e as telas locais continuam funcionando normalmente.
    private void IniciarServidorDoPainel(CaminhosDoApp caminhos)
    {
        _servidorDoPainel = new ServidorDoPainel();
        _ = _servidorDoPainel.IniciarAsync(caminhos.BancoDeDados);
    }
}
