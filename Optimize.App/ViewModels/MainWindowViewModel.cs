using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Optimize.App.Services;
using OptimizePro.Painel;
using OptimizePro.Sincronizacao;

namespace Optimize.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly CultureInfo CulturaRelogio = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IFabricaDeViewModels _fabricaDeViewModels;
    private readonly SessaoDoPainel _sessao;
    private readonly SincronizacaoService _sincronizacao;
    private readonly IUsuarioRepository _usuarios;
    private readonly Timer _timerDoRelogio;

    /// <summary>App.axaml.cs assina isto pra trocar de janela (fecha a MainWindow, mostra o login de novo) — o ViewModel não conhece <c>IClassicDesktopStyleApplicationLifetime</c> nem deveria.</summary>
    public event Action? SolicitouSaida;

    [ObservableProperty]
    public partial ViewModelBase TelaAtual { get; set; }

    /// <summary>Qual entrada de <see cref="InfoDasTelas"/> alimenta o cabeçalho — separado de <see cref="TelaAtual"/> (o ViewModel em si) porque o cabeçalho só precisa de título/apoio/ícone, não do VM inteiro.</summary>
    [ObservableProperty]
    public partial TipoDeTela TelaSelecionada { get; set; } = TipoDeTela.Moldes;

    public InfoDeTela InfoDaTelaAtual => InfoDasTelas.Todas[TelaSelecionada];

    partial void OnTelaSelecionadaChanged(TipoDeTela value) => OnPropertyChanged(nameof(InfoDaTelaAtual));

    // TODO: refletir o status real assim que IWhatsAppSidecarClient existir (§14).
    [ObservableProperty]
    public partial string StatusWhatsApp { get; set; } = "desconectado";

    /// <summary>Porte de <c>useRelogio.ts</c> — data/hora do cabeçalho, atualizadas de meio em meio minuto (o relógio só mostra hora:minuto, não há o que ganhar olhando mais vezes).</summary>
    [ObservableProperty]
    public partial string RelogioData { get; set; } = "";

    [ObservableProperty]
    public partial string RelogioHora { get; set; } = "";

    /// <summary>Lida direto do assembly (Version/Company no .csproj) em vez de hardcoded aqui — atualiza sozinho a cada release, sem precisar lembrar de trocar em dois lugares.</summary>
    public string VersaoDoApp
    {
        get
        {
            var assembly = Assembly.GetExecutingAssembly();
            var versao = assembly.GetName().Version;
            var empresa = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "";
            return $"v{versao?.ToString(3)} — Desenvolvido por {empresa}";
        }
    }

    /// <summary>Botão "Sair" só faz sentido se o gate estiver ativo (alguém realmente logou) — sem isso, "sair" levaria pra uma tela de login que nem deveria aparecer.</summary>
    public bool PodeSair => _sessao.UsuarioAtual is not null;

    [ObservableProperty]
    public partial bool Sincronizando { get; set; }

    public IReadOnlyList<ItemDeMenu> ItensDeMenu { get; } =
    [
        new(TipoDeTela.Moldes, "Moldes", "Produção"),
        new(TipoDeTela.Projetos, "Projetos", "Produção"),
        new(TipoDeTela.Encaixe, "Encaixe", "Produção"),
        new(TipoDeTela.Vetor, "Vetor", "Produção"),
        new(TipoDeTela.Impressoras, "Impressoras", "Produção"),
        new(TipoDeTela.Maquinas, "Máquinas", "Produção"),
        new(TipoDeTela.Historico, "Histórico", "Produção"),
        new(TipoDeTela.Reposicao, "Reposição", "Produção"),
        new(TipoDeTela.Pedidos, "Pedidos", "Produção"),
        new(TipoDeTela.OrdensDeServico, "Ordens de Serviço", "Produção"),
        new(TipoDeTela.Disparo, "Disparo", "Comunicação"),
        new(TipoDeTela.Configuracoes, "Configurações", "Comunicação"),
    ];

    public MainWindowViewModel(
        IFabricaDeViewModels fabricaDeViewModels, INavegador navegador, SessaoDoPainel sessao,
        SincronizacaoService sincronizacao, IUsuarioRepository usuarios)
    {
        _fabricaDeViewModels = fabricaDeViewModels;
        _sessao = sessao;
        _sincronizacao = sincronizacao;
        _usuarios = usuarios;
        TelaAtual = fabricaDeViewModels.Criar(TipoDeTela.Moldes);
        navegador.Navegado += (tipo, parametro) =>
        {
            if (!PodeNavegarPara(tipo)) return;
            TelaAtual = _fabricaDeViewModels.Criar(tipo, parametro);
            TelaSelecionada = tipo;
        };

        AtualizarRelogio();
        _timerDoRelogio = new Timer(TimeSpan.FromSeconds(30)) { AutoReset = true };
        // Elapsed dispara numa thread do pool — as propriedades observáveis (bind na UI)
        // só podem ser tocadas na UI thread.
        _timerDoRelogio.Elapsed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(AtualizarRelogio);
        _timerDoRelogio.Start();
    }

    private void AtualizarRelogio()
    {
        var agora = DateTime.Now;
        RelogioData = agora.ToString("ddd, dd 'de' MMM", CulturaRelogio).Replace(".", "");
        RelogioHora = agora.ToString("HH:mm", CulturaRelogio);
    }

    // Gate de módulos (§25) — espelha o que o painel remoto já faz (§24.6/§24.7): os mesmos 6
    // módulos de "frota de impressoras" ficam escondidos aqui se o usuário logado não tiver
    // liberação. Moldes/Projetos/Encaixe/Vetor/Disparo/Configurações NUNCA são módulo — são o
    // produto original, fora do escopo do painel do proprietário, sempre visíveis.
    // UsuarioAtual null = ninguém logou ou o gate está desativado (sem usuário cacheado
    // localmente ainda) — nesse caso mostra tudo, igual ao comportamento de sempre.
    private bool PodeVer(ModuloDoPainel modulo) => _sessao.UsuarioAtual is null || _sessao.UsuarioAtual.ModulosLiberados.Contains(modulo);

    public bool PodeVerImpressoras => PodeVer(ModuloDoPainel.Impressoras);
    public bool PodeVerMaquinas => PodeVer(ModuloDoPainel.Maquinas);
    public bool PodeVerHistorico => PodeVer(ModuloDoPainel.Historico);
    public bool PodeVerReposicao => PodeVer(ModuloDoPainel.Reposicao);
    public bool PodeVerPedidos => PodeVer(ModuloDoPainel.Pedidos);
    public bool PodeVerOrdensDeServico => PodeVer(ModuloDoPainel.OrdensDeServico);

    private bool PodeNavegarPara(TipoDeTela tipo) => tipo switch
    {
        TipoDeTela.Impressoras => PodeVerImpressoras,
        TipoDeTela.Maquinas => PodeVerMaquinas,
        TipoDeTela.Historico => PodeVerHistorico,
        TipoDeTela.Reposicao => PodeVerReposicao,
        TipoDeTela.Pedidos => PodeVerPedidos,
        TipoDeTela.OrdensDeServico => PodeVerOrdensDeServico,
        _ => true,
    };

    [RelayCommand]
    private void NavegarPara(ItemDeMenu item)
    {
        if (!PodeNavegarPara(item.Tipo)) return;
        TelaAtual = _fabricaDeViewModels.Criar(item.Tipo);
        TelaSelecionada = item.Tipo;
    }

    [RelayCommand]
    private void NavegarParaTipo(TipoDeTela tipo)
    {
        if (!PodeNavegarPara(tipo)) return;
        TelaAtual = _fabricaDeViewModels.Criar(tipo);
        TelaSelecionada = tipo;
    }

    [RelayCommand]
    private void Sair() => SolicitouSaida?.Invoke();

    /// <summary>
    /// Dispara a sincronização na hora (sem esperar o ciclo de 10 min do
    /// <see cref="SincronizadorEmSegundoPlano"/>) e recarrega o usuário logado do cache local
    /// já atualizado — pedido do usuário: "clica e a tela se ajusta ao que está liberado", sem
    /// precisar sair e logar de novo. Se o usuário foi desativado ou excluído remotamente
    /// nesse meio-tempo, o efeito é o mesmo de "Sair" — não faz sentido continuar mostrando
    /// uma sessão que não existe mais do lado de quem manda (a Central, desde a §24.7).
    /// </summary>
    [RelayCommand]
    private async Task Sincronizar()
    {
        if (Sincronizando) return;
        Sincronizando = true;
        try
        {
            await _sincronizacao.SincronizarAsync();

            if (_sessao.UsuarioAtual is not { } usuarioLogado) return;

            var atualizado = await _usuarios.ObterAsync(usuarioLogado.Id);
            if (atualizado is null || !atualizado.Habilitado)
            {
                SolicitouSaida?.Invoke();
                return;
            }

            _sessao.UsuarioAtual = atualizado;
            AtualizarVisibilidadeDosModulos();
        }
        finally
        {
            Sincronizando = false;
        }
    }

    private void AtualizarVisibilidadeDosModulos()
    {
        OnPropertyChanged(nameof(PodeVerImpressoras));
        OnPropertyChanged(nameof(PodeVerMaquinas));
        OnPropertyChanged(nameof(PodeVerHistorico));
        OnPropertyChanged(nameof(PodeVerReposicao));
        OnPropertyChanged(nameof(PodeVerPedidos));
        OnPropertyChanged(nameof(PodeVerOrdensDeServico));

        // Se a tela atual acabou de ficar escondida (módulo removido nesta sincronização),
        // não deixa a pessoa continuar olhando pra ela — manda pra Moldes, sempre acessível.
        if (!PodeNavegarPara(TelaSelecionada))
        {
            TelaAtual = _fabricaDeViewModels.Criar(TipoDeTela.Moldes);
            TelaSelecionada = TipoDeTela.Moldes;
        }
    }
}
