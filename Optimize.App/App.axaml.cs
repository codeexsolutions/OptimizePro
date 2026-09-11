using System.Linq;
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
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private CaminhosDoApp? _caminhos;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            var caminhos = new CaminhosDoApp();
            _caminhos = caminhos;

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

                    // Gate de login por módulo (§25) — a mesma tabela Usuarios acima, agora
                    // também lida localmente pra autenticar OFFLINE quem abre o app desktop.
                    services.AddScoped<IUsuarioRepository, UsuarioRepository>();
                    services.AddScoped<IAutenticacaoService, AutenticacaoService>();
                    services.AddSingleton<SessaoDoPainel>();

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
                    services.AddTransient<LoginDoPainelViewModel>();
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
                EntrarNoAppPrincipal(exibirManualmente: false);
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

                    EntrarNoAppPrincipal(exibirManualmente: true);
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

    private MainWindow CriarMainWindow()
    {
        var viewModel = _escopoDaSessao!.ServiceProvider.GetRequiredService<MainWindowViewModel>();
        var window = new MainWindow { DataContext = viewModel };

        // Botão "Sair" (§25) — mostra a janela de login NOVA antes de fechar esta (nunca os
        // dois zero janelas abertas ao mesmo tempo; ShutdownMode.OnLastWindowClose fecharia o
        // app inteiro se isso acontecesse por um instante).
        viewModel.SolicitouSaida += () =>
        {
            _escopoDaSessao!.ServiceProvider.GetRequiredService<SessaoDoPainel>().UsuarioAtual = null;
            MostrarTelaDeLogin(exibirManualmente: true);
            window.Close();
        };

        return window;
    }

    // Gate de login por módulo (§25) — só entra em cena se já existir pelo menos um usuário do
    // painel cacheado localmente (sincronizado da Central, ver SincronizacaoService). Instalação
    // nova, que nunca configurou usuários no painel remoto, continua abrindo direto — não trava
    // quem não usa esse recurso. "exibirManualmente" existe porque, chamado durante o startup
    // síncrono (licença já liberada), o Avalonia mostra "desktop.MainWindow" sozinho; chamado de
    // dentro de um Closed/evento (depois do startup), precisa de Show() explícito.
    private void EntrarNoAppPrincipal(bool exibirManualmente)
    {
        var painelDb = _escopoDaSessao!.ServiceProvider.GetRequiredService<PainelDbContext>();

        if (!painelDb.Usuarios.Any())
        {
            AbrirMainWindow(exibirManualmente);
            return;
        }

        MostrarTelaDeLogin(exibirManualmente);
    }

    // Reaproveitada tanto no gate inicial quanto no botão "Sair" (volta pra cá sem fechar o
    // processo) — por isso não checa de novo "existem usuários": quem chama já sabe que sim.
    private void MostrarTelaDeLogin(bool exibirManualmente)
    {
        var loginViewModel = _escopoDaSessao!.ServiceProvider.GetRequiredService<LoginDoPainelViewModel>();
        var loginWindow = new LoginDoPainelWindow(loginViewModel);
        loginWindow.Closed += (_, _) =>
        {
            if (!loginViewModel.Autenticado)
            {
                _desktop!.Shutdown();
                return;
            }

            AbrirMainWindow(exibirManualmente: true);
        };

        _desktop!.MainWindow = loginWindow;
        if (exibirManualmente) loginWindow.Show();
    }

    private void AbrirMainWindow(bool exibirManualmente)
    {
        var mainWindow = CriarMainWindow();
        _desktop!.MainWindow = mainWindow;
        if (exibirManualmente) mainWindow.Show();

        // Só na primeira vez: reentrar aqui via "Sair" não deve subir um segundo servidor do
        // painel na mesma porta por cima do que já está rodando.
        if (_servidorDoPainel is null) IniciarServidorDoPainel(_caminhos!);
    }

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
