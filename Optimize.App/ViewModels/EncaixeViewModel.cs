using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OptimizePro.Core;
using OptimizePro.Core.Encaixe;
using OptimizePro.Core.Encaixe.Busca;
using OptimizePro.Core.Moldes;
using OptimizePro.Services.Encaixe;

namespace Optimize.App.ViewModels;

/// <summary>
/// Tela mais complexa (§9.3) — arrasta/escolhe arquivos (DXF/PLT/SVG/PDF) direto na tela,
/// configura o tecido e dispara a busca via <see cref="IEncaixeService"/>. Porte do fluxo de
/// <c>encaixe.js</c> do projeto original: o arquivo já entra pronto pra encaixar, sem precisar
/// de um Molde cadastrado antes (§ATUALIZACAO 01/09/2026 — "fluxo de arquivo direto"). Imagem
/// raster (PNG/JPG) com remoção de fundo fica pro próximo incremento — só os formatos
/// vetoriais (mesmos da Moldes wizard) por ora. Só o motor automático (contorno+retângulo,
/// sem blocos/faixas/NFP — ver <c>GeradorDeReceitas</c>) está disponível; o desenho do
/// resultado é simplificado (retângulos delimitadores, não a arte vetorial de cada peça —
/// próximo incremento planejado, ver <c>PosicaoVisual</c>).
/// </summary>
public partial class EncaixeViewModel : ViewModelBase
{
    private static readonly string[] Paleta =
    [
        "#F2762E", "#3D8BFD", "#2FBF71", "#D63384", "#9C6ADE", "#FFC145", "#20C0C8", "#E8590C",
    ];

    private static readonly string[] ExtensoesAceitas = ["dxf", "plt", "svg", "pdf", "png", "jpg", "jpeg"];

    private readonly IReadOnlyList<ILeitorDeMolde> _leitores;
    private readonly IEncaixeService _encaixeService;
    private readonly IEncaixeMemoriaService _memoriaService;
    private int _proximoId;
    private CancellationTokenSource? _cts;
    private EncaixeGuardadoDto? _guardadoAnterior;

    public ObservableCollection<PecaDeArquivoEncaixe> Arquivos { get; } = [];
    public ObservableCollection<PosicaoVisual> Posicoes { get; } = [];
    public ObservableCollection<MarcaDeRegua> ReguaHorizontal { get; } = [];
    public ObservableCollection<MarcaDeRegua> ReguaVertical { get; } = [];

    [ObservableProperty]
    public partial double LarguraTecidoCm { get; set; } = 150;

    /// <summary>Espaço/folga entre peças — mostrado em mm na tela (igual ao original), convertido pra cm só na hora de montar <see cref="ConfiguracaoDeEncaixe"/>.</summary>
    [ObservableProperty]
    public partial double EspacoMm { get; set; } = 5;

    public double EspacoCm => EspacoMm / 10.0;

    [ObservableProperty]
    public partial double MargemCm { get; set; } = 1;

    /// <summary>"Bancada" (porte de <c>encaixeMotor.js</c>) — comprimento máximo do rolo em cm; nenhuma peça cruza essa linha. Null/0 = sem limite (padrão de sempre).</summary>
    [ObservableProperty]
    public partial double? ComprimentoBancadaCm { get; set; }

    /// <summary>"Como encaixar" — automático deixa contorno+retângulo disputarem; forçar um só é escolha explícita.</summary>
    [ObservableProperty]
    public partial ModoDeEncaixe ModoDeEncaixe { get; set; } = ModoDeEncaixe.Automatico;

    public IReadOnlyList<ModoDeEncaixe> OpcoesDeModo { get; } = [ModoDeEncaixe.Automatico, ModoDeEncaixe.SempreContorno, ModoDeEncaixe.SempreCaixa];

    /// <summary>
    /// Modo alternativo de encaixe (pedido explícito pelo usuário, teste empírico): em vez de
    /// misturar todos os tipos de peça numa busca só, encaixa cada tipo isolado (todas as
    /// cópias de UM molde de cada vez — "todas as frentes juntas, depois todas as costas etc.")
    /// e empilha o resultado de cada tipo no tecido. Desmarcado (padrão) mantém o comportamento
    /// de sempre: uma busca só, todos os tipos competindo/misturando juntos.
    /// </summary>
    [ObservableProperty]
    public partial bool AgruparPecasIguais { get; set; }

    /// <summary>Unidade forçada na leitura do molde vetorial (null = automático, detecta do próprio arquivo) — §8.1/§9.3.</summary>
    [ObservableProperty]
    public partial string? UnidadeDoMolde { get; set; }

    public IReadOnlyList<string?> OpcoesDeUnidade { get; } = [null, "mm", "cm", "m", "pol"];

    [ObservableProperty]
    public partial ModoLeituraVetor ModoDeLeituraDoVetor { get; set; } = ModoLeituraVetor.Marcador;

    public IReadOnlyList<ModoLeituraVetor> OpcoesDeLeituraDoVetor { get; } = [ModoLeituraVetor.Marcador, ModoLeituraVetor.Inteiro];

    [ObservableProperty]
    public partial int TempoDeBuscaSegundos { get; set; } = 8;

    /// <summary>
    /// Giro padrão pras peças que entrarem DAQUI PRA FRENTE (§11.11) — mudar isto não afeta as
    /// já adicionadas, cada peça guarda o próprio giro editável na linha. "Mantém sentido" é o
    /// mais seguro pra tecido com fio/estampa direcional; "Livre" rende mais aproveitamento em
    /// malha lisa/sem sentido — medido ~12% de economia real numa batelada de peças de camiseta.
    /// </summary>
    [ObservableProperty]
    public partial TipoDeGiro GiroDeTodasAsPecas { get; set; } = TipoDeGiro.MantemSentido;

    public IReadOnlyList<TipoDeGiro> OpcoesDeGiro { get; } = [TipoDeGiro.MantemSentido, TipoDeGiro.Fixa, TipoDeGiro.Livre];

    /// <summary>Mesmas opções, mas com <c>null</c> ("usa o padrão") na frente — pro seletor de giro POR PEÇA, nunca pro seletor geral.</summary>
    public IReadOnlyList<TipoDeGiro?> OpcoesDeGiroPorPeca { get; } = [null, TipoDeGiro.MantemSentido, TipoDeGiro.Fixa, TipoDeGiro.Livre];

    [ObservableProperty]
    public partial bool EmBusca { get; set; }

    [ObservableProperty]
    public partial bool CarregandoArquivos { get; set; }

    [ObservableProperty]
    public partial int ArquivosProcessados { get; set; }

    [ObservableProperty]
    public partial int ArquivosParaProcessar { get; set; }

    [ObservableProperty]
    public partial int Tentativas { get; set; }

    [ObservableProperty]
    public partial double MelhorConsumoCm { get; set; }

    [ObservableProperty]
    public partial string? MensagemDeErro { get; set; }

    [ObservableProperty]
    public partial ResultadoDeEncaixe? Resultado { get; set; }

    /// <summary>Porte do banner "O melhor encaixe já conseguido com estas mesmas peças gastou X — este saiu Y" (`encaixe_guardados`, §12) — só aparece quando o resultado desta busca ficou pior que o melhor físico já salvo pra este trabalho EXATO.</summary>
    [ObservableProperty]
    public partial bool TemMelhorGuardado { get; set; }

    [ObservableProperty]
    public partial double MelhorGuardadoConsumoCm { get; set; }

    [ObservableProperty]
    public partial double CanvasLarguraPx { get; set; }

    [ObservableProperty]
    public partial double CanvasAlturaPx { get; set; }

    public bool TemResultado => Resultado is not null;
    public bool TemArquivos => Arquivos.Count > 0;

    partial void OnResultadoChanged(ResultadoDeEncaixe? value) => OnPropertyChanged(nameof(TemResultado));

    public EncaixeViewModel(IEnumerable<ILeitorDeMolde> leitores, IEncaixeService encaixeService, IEncaixeMemoriaService memoriaService)
    {
        _leitores = [.. leitores];
        _encaixeService = encaixeService;
        _memoriaService = memoriaService;
    }

    /// <summary>
    /// Chamado pelo code-behind da View depois de escolher/arrastar arquivos (§8.3 — a leitura
    /// em si fica na View porque depende do <c>TopLevel</c>/<c>StorageProvider</c>/drag-drop,
    /// que não pertencem à ViewModel). Cada peça FECHADA do arquivo vira uma linha — um DXF/SVG
    /// pode conter várias.
    /// </summary>
    /// <remarks>
    /// Processa em paralelo (limitado — arte de impressão em JPG/PNG real chega a dezenas de
    /// megapixels e um arquivo sozinho pode levar mais de 1s pra decodificar+traçar; sequencial,
    /// um lote de 25 arquivos reais mediu ~37s, o suficiente pra parecer travado sem nenhum
    /// aviso na tela). Só a leitura roda fora da thread de UI — <see cref="Arquivos"/> só é
    /// alterada depois, de volta na UI, porque <see cref="ObservableCollection{T}"/> não é
    /// thread-safe.
    /// </remarks>
    public async Task AdicionarArquivosAsync(IReadOnlyList<(string Nome, byte[] Bytes)> arquivos)
    {
        if (arquivos.Count == 0) return;

        CarregandoArquivos = true;
        MensagemDeErro = null;
        ArquivosProcessados = 0;
        ArquivosParaProcessar = arquivos.Count;

        var avisos = new ConcurrentBag<string>();
        var processados = 0;
        var paralelismo = Math.Clamp(Environment.ProcessorCount, 2, 6);
        using var semaforo = new SemaphoreSlim(paralelismo);

        try
        {
            var tarefas = arquivos.Select(async arquivo =>
            {
                await semaforo.WaitAsync();
                try
                {
                    return await LerArquivoAsync(arquivo.Nome, arquivo.Bytes, avisos);
                }
                finally
                {
                    var atual = Interlocked.Increment(ref processados);
                    Dispatcher.UIThread.Post(() => ArquivosProcessados = atual);
                    semaforo.Release();
                }
            });

            var resultados = await Task.WhenAll(tarefas);

            foreach (var pecas in resultados)
                foreach (var peca in pecas)
                    Arquivos.Add(peca);
        }
        finally
        {
            if (!avisos.IsEmpty) MensagemDeErro = string.Join(" ", avisos);
            CarregandoArquivos = false;
            OnPropertyChanged(nameof(TemArquivos));
        }
    }

    /// <summary>Lê um único arquivo (fora da UI) e devolve as peças prontas — quem chama decide quando/como colocá-las em <see cref="Arquivos"/>.</summary>
    private async Task<List<PecaDeArquivoEncaixe>> LerArquivoAsync(string nomeDoArquivo, byte[] bytes, ConcurrentBag<string> avisos)
    {
        var extensao = Path.GetExtension(nomeDoArquivo).TrimStart('.').ToLowerInvariant();
        var pecas = new List<PecaDeArquivoEncaixe>();

        if (LeitorDeImagemDeEncaixe.SuportaExtensao(extensao))
        {
            try
            {
                var pecaLida = await Task.Run(() => LeitorDeImagemDeEncaixe.Ler(bytes, nomeDoArquivo));
                var doNome = LeitorDeQuantidadeDoNome.Ler(pecaLida.Nome);

                pecas.Add(new PecaDeArquivoEncaixe
                {
                    Id = $"arq{Interlocked.Increment(ref _proximoId)}",
                    Nome = doNome.Nome,
                    NomeDoArquivo = nomeDoArquivo,
                    Largura = pecaLida.LarguraCm,
                    Altura = pecaLida.AlturaCm,
                    Contorno = pecaLida.Contorno,
                    Origem = pecaLida.Origem,
                    QuantidadeVeioDoNome = doNome.VeioDoNome,
                    Quantidade = doNome.Quantidade,
                });
            }
            catch (Exception ex)
            {
                avisos.Add($"\"{nomeDoArquivo}\": {ex.Message}");
            }

            return pecas;
        }

        var leitor = _leitores.FirstOrDefault(l => l.SuportaExtensao(extensao));

        if (leitor is null)
        {
            avisos.Add($"\"{nomeDoArquivo}\": formato '.{extensao}' não suportado (use DXF, PLT, SVG, PDF, PNG ou JPG).");
            return pecas;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            var resultado = await leitor.LerAsync(stream, new OpcoesLeituraMolde(UnidadeDoMolde, ModoDeLeituraDoVetor));

            if (resultado.Erro is not null)
            {
                avisos.Add($"\"{nomeDoArquivo}\": {resultado.Erro}");
                return pecas;
            }

            if (resultado.Pecas.Count == 0)
            {
                avisos.Add($"\"{nomeDoArquivo}\": não achei nenhuma peça fechada no arquivo.");
                return pecas;
            }

            foreach (var pecaLidaDeVetor in resultado.Pecas)
            {
                var doNome = LeitorDeQuantidadeDoNome.Ler(pecaLidaDeVetor.Nome);
                pecas.Add(new PecaDeArquivoEncaixe
                {
                    Id = $"arq{Interlocked.Increment(ref _proximoId)}",
                    Nome = doNome.Nome,
                    NomeDoArquivo = nomeDoArquivo,
                    Largura = Math.Round(pecaLidaDeVetor.LarguraCm, 1),
                    Altura = Math.Round(pecaLidaDeVetor.AlturaCm, 1),
                    Contorno = pecaLidaDeVetor.Contorno,
                    Origem = $"{extensao.ToUpperInvariant()} · {resultado.Unidade}",
                    QuantidadeVeioDoNome = doNome.VeioDoNome,
                    Quantidade = doNome.Quantidade,
                });
            }

            foreach (var aviso in resultado.Avisos)
                avisos.Add($"\"{nomeDoArquivo}\": {aviso}");
        }
        catch (Exception ex)
        {
            avisos.Add($"\"{nomeDoArquivo}\": {ex.Message}");
        }

        return pecas;
    }

    [RelayCommand]
    private void RemoverArquivo(PecaDeArquivoEncaixe peca)
    {
        Arquivos.Remove(peca);
        OnPropertyChanged(nameof(TemArquivos));
    }

    [RelayCommand]
    private void LimparArquivos()
    {
        Arquivos.Clear();
        Resultado = null;
        Posicoes.Clear();
        LarguraOcupadaCm = SobraEsquerdaCm = SobraDireitaCm = SobraTotalCm = SobraCentralizadaCm = 0;
        BarraEsquerdaPx = BarraOcupadaPx = BarraDireitaPx = 0;
        AreaDasPecasM2 = SobraDeTecidoM2 = 0;
        ResumoDoEncaixe = "";
        MensagemDeErro = null;
        OnPropertyChanged(nameof(TemArquivos));
    }

    public bool AceitaExtensao(string nomeDoArquivo) =>
        ExtensoesAceitas.Contains(Path.GetExtension(nomeDoArquivo).TrimStart('.').ToLowerInvariant());

    [RelayCommand]
    private async Task FazerEncaixeAsync()
    {
        var itens = Arquivos
            .Where(p => p.Quantidade > 0)
            .Select(p => new PecaParaEncaixar(p.Id, p.Contorno, p.Quantidade, p.Giro ?? GiroDeTodasAsPecas))
            .ToList();

        if (itens.Count == 0)
        {
            MensagemDeErro = "Adicione ao menos um arquivo com quantidade maior que zero.";
            return;
        }

        _cts = new CancellationTokenSource();
        EmBusca = true;
        MensagemDeErro = null;
        Tentativas = 0;
        MelhorConsumoCm = 0;
        Resultado = null;
        Posicoes.Clear();
        TemMelhorGuardado = false;

        var chaveExata = ChaveExataDeTrabalho.Calcular(
            itens.Select(i => new ItemParaChaveExata(i.Id, i.Contorno, i.Quantidade, i.Giro)).ToList(),
            LarguraTecidoCm, EspacoCm, MargemCm);

        var config = new ConfiguracaoDeEncaixe(LarguraTecidoCm, EspacoCm, MargemCm, TempoDeBuscaSegundos * 1000L, 1_500, ModoDeEncaixe,
            ComprimentoBancadaCm is > 0 ? ComprimentoBancadaCm : null);
        var progresso = new Progress<AndamentoDoEncaixe>(a =>
        {
            Tentativas = a.Tentativas;
            MelhorConsumoCm = a.MelhorConsumoCm;
        });

        try
        {
            Resultado = AgruparPecasIguais
                ? await BuscarEncaixeAgrupadoAsync(itens, config, progresso, _cts.Token)
                : await _encaixeService.BuscarMelhorEncaixeAsync(itens, config, progresso, _cts.Token);
            MontarPosicoesVisuais();
            await AvaliarContraGuardadoAsync(chaveExata, Resultado);
        }
        catch (OperationCanceledException)
        {
            // Cancelado antes de qualquer tentativa completar — nada pra mostrar.
        }
        catch (Exception ex)
        {
            MensagemDeErro = $"Não foi possível concluir o encaixe: {ex.Message}";
        }
        finally
        {
            EmBusca = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Porte do modo "agrupar peças iguais" (§9.3, teste empírico pedido pelo usuário: "todas
    /// as frentes, todas as costas, todas as mangas etc. — agrupamento por peças iguais é bem
    /// mais efetivo"). Cada entrada de <paramref name="itens"/> já É um tipo de peça distinto
    /// (um arquivo = um molde, com a quantidade cobrindo todas as cópias idênticas dele — ver
    /// <c>_proximoId</c>/<c>PecaDeArquivoEncaixe.Id</c>), então não precisa reagrupar por forma:
    /// só roda uma busca isolada POR TIPO (maior área primeiro, mesmo critério padrão do motor)
    /// e empilha o resultado de cada uma embaixo da anterior. Margem só entra uma vez, no topo
    /// e no fundo da pilha inteira — os sub-encaixes rodam sem margem própria pra não desperdiçar
    /// tecido repetindo a folga a cada tipo.
    /// </summary>
    private async Task<ResultadoDeEncaixe> BuscarEncaixeAgrupadoAsync(
        IReadOnlyList<PecaParaEncaixar> itens, ConfiguracaoDeEncaixe config, IProgress<AndamentoDoEncaixe> progresso, CancellationToken ct)
    {
        var grupos = itens
            .OrderByDescending(p =>
            {
                var caixa = Geometria.CaixaDeContorno(p.Contorno);
                return caixa.Largura * caixa.Altura;
            })
            .ToList();

        var tempoPorGrupoMs = Math.Max(1_000L, config.TempoMaximoMs / grupos.Count);
        var paredePorGrupoMs = Math.Min(config.MsSemGanhoParaParedeMs, Math.Max(300L, tempoPorGrupoMs / 2));

        var posicoes = new List<ItemDeResultado>();
        var naoEncaixados = new List<string>();
        var receitasPorGrupo = new List<string>();
        var tentativasTotais = 0;
        var areaRealTotal = 0.0;
        var alturaAcumuladaCm = 0.0;

        foreach (var grupo in grupos)
        {
            ct.ThrowIfCancellationRequested();

            var configGrupo = config with { MargemCm = 0, TempoMaximoMs = tempoPorGrupoMs, MsSemGanhoParaParedeMs = paredePorGrupoMs };
            var tentativasAntesDoGrupo = tentativasTotais;
            var alturaAntesDoGrupo = alturaAcumuladaCm;

            var progressoDoGrupo = new Progress<AndamentoDoEncaixe>(a => progresso.Report(
                new AndamentoDoEncaixe(tentativasAntesDoGrupo + a.Tentativas, alturaAntesDoGrupo + a.MelhorConsumoCm, a.TempoDecorridoMs)));

            var resultadoGrupo = await _encaixeService.BuscarMelhorEncaixeAsync([grupo], configGrupo, progressoDoGrupo, ct);

            foreach (var posicao in resultadoGrupo.Posicoes)
                posicoes.Add(posicao with { Y = posicao.Y + alturaAcumuladaCm });

            naoEncaixados.AddRange(resultadoGrupo.ItensNaoEncaixados);
            if (resultadoGrupo.Posicoes.Count > 0)
                receitasPorGrupo.Add($"{grupo.Id}:{resultadoGrupo.ReceitaVencedora}");

            tentativasTotais += resultadoGrupo.Tentativas;
            areaRealTotal += resultadoGrupo.AreaRealCm2;
            alturaAcumuladaCm += resultadoGrupo.ConsumoCm;
        }

        var consumoFinal = alturaAcumuladaCm + config.MargemCm * 2;
        var aproveitamento = consumoFinal > 0 && config.LarguraTecidoCm > 0
            ? Math.Clamp(areaRealTotal / (config.LarguraTecidoCm * consumoFinal) * 100, 0, 100)
            : 0;

        return new ResultadoDeEncaixe(
            posicoes, naoEncaixados, consumoFinal, aproveitamento, tentativasTotais,
            $"Agrupado por peça ({grupos.Count} tipos) · {string.Join(" · ", receitasPorGrupo)}", areaRealTotal);
    }

    [RelayCommand]
    private void Parar() => _cts?.Cancel();

    /// <summary>
    /// Porte de <c>encaixe_guardados</c> (§3.6/§12) — compara o resultado desta busca com o
    /// melhor encaixe FÍSICO já obtido pra este trabalho EXATO (mesmas peças/quantidades/giro/
    /// tecido/folga/margem — não só "parecido"). Se este saiu pior, avisa e guarda o anterior
    /// pronto pra usar de volta; se saiu igual ou melhor, vira o novo recorde salvo. Falha de
    /// memória nunca derruba o resultado do encaixe em si (mesma regra do resto do sistema).
    /// </summary>
    private async Task AvaliarContraGuardadoAsync(string chaveExata, ResultadoDeEncaixe resultado)
    {
        try
        {
            _guardadoAnterior = await _memoriaService.BuscarGuardadoAsync(chaveExata);

            if (_guardadoAnterior is { } guardado && resultado.ConsumoCm > guardado.Consumo + 0.05)
            {
                MelhorGuardadoConsumoCm = guardado.Consumo;
                TemMelhorGuardado = true;
            }

            var posicoesJson = JsonSerializer.Serialize(resultado.Posicoes);
            await _memoriaService.GuardarSeMelhorAsync(new EncaixeGuardadoDto(
                chaveExata, null, LarguraTecidoCm, EspacoCm, MargemCm,
                resultado.ConsumoCm, resultado.AproveitamentoPercentual, null, posicoesJson, resultado.ReceitaVencedora));
        }
        catch
        {
            // idem — memória não é requisito pro resultado do encaixe em si.
        }
    }

    /// <summary>
    /// "PDF em tamanho real" (§11) — chamado do code-behind da View, que cuida do diálogo de
    /// salvar (mesmo padrão de <see cref="VetorViewModel.GerarPdf"/>). Devolve null se ainda
    /// não há resultado.
    /// </summary>
    public byte[]? GerarPdf()
    {
        if (Resultado is not { } resultado) return null;

        var contornosPorPeca = Arquivos.ToDictionary(a => a.Id, a => a.Contorno);
        return ExportadorDeEncaixeParaPdf.Exportar(LarguraTecidoCm, resultado.ConsumoCm, resultado.Posicoes, contornosPorPeca,
            ComprimentoBancadaCm is > 0 ? ComprimentoBancadaCm : null);
    }

    [RelayCommand]
    private void UsarMelhorDeAntes()
    {
        if (_guardadoAnterior is not { } guardado) return;

        var posicoes = JsonSerializer.Deserialize<List<ItemDeResultado>>(guardado.PosicoesJson) ?? [];
        var larguraTecido = guardado.LarguraTecido ?? LarguraTecidoCm;
        var aproveitamento = guardado.Aproveitamento ?? 0;
        var areaReal = aproveitamento > 0 && larguraTecido > 0 ? aproveitamento / 100.0 * larguraTecido * guardado.Consumo : 0;

        Resultado = new ResultadoDeEncaixe(posicoes, [], guardado.Consumo, aproveitamento, 0, guardado.Receita ?? "", areaReal);
        MontarPosicoesVisuais();
        TemMelhorGuardado = false;
    }

    private void MontarPosicoesVisuais()
    {
        Posicoes.Clear();
        ReguaHorizontal.Clear();
        ReguaVertical.Clear();
        if (Resultado is not { } resultado || resultado.Posicoes.Count == 0) return;

        const double maxLarguraPx = 760;
        var escala = LarguraTecidoCm > 0 ? Math.Min(6, maxLarguraPx / LarguraTecidoCm) : 3;

        CanvasLarguraPx = LarguraTecidoCm * escala;
        CanvasAlturaPx = Math.Max(resultado.ConsumoCm * escala, 40);

        MontarReguas(escala, resultado.ConsumoCm);

        var corPorGrupo = new Dictionary<string, IBrush>();

        foreach (var posicao in resultado.Posicoes)
        {
            var grupo = posicao.PecaId.Split('#')[0];
            if (!corPorGrupo.TryGetValue(grupo, out var cor))
            {
                cor = new SolidColorBrush(Color.Parse(Paleta[corPorGrupo.Count % Paleta.Length]));
                corPorGrupo[grupo] = cor;
            }

            var contornoBase = Arquivos.FirstOrDefault(a => a.Id == grupo)?.Contorno
                ?? [new PontoXY(0, 0), new PontoXY(posicao.LarguraCm, 0), new PontoXY(posicao.LarguraCm, posicao.AlturaCm), new PontoXY(0, posicao.AlturaCm)];

            var geometria = MontarGeometriaRotacionada(contornoBase, posicao.RotacaoGraus, posicao.X, posicao.Y, escala);
            var centroX = (posicao.X + posicao.LarguraCm / 2) * escala;
            var centroY = (posicao.Y + posicao.AlturaCm / 2) * escala;

            Posicoes.Add(new PosicaoVisual(geometria, centroX, centroY, cor, grupo));
        }

        MontarSobraLateral(resultado);
    }

    /// <summary>Marca a cada 10cm (traço fino) e a cada 50cm (traço + rótulo, trocando pra "m" a partir de 1m) — mesma escala px/cm usada pras peças, pra régua e desenho nunca desalinharem.</summary>
    private void MontarReguas(double escalaPxPorCm, double consumoCm)
    {
        const double passoMenorCm = 10;
        const double passoMaiorCm = 50;

        for (var cm = 0.0; cm <= LarguraTecidoCm + 0.01; cm += passoMenorCm)
        {
            var maior = cm % passoMaiorCm < 0.01;
            ReguaHorizontal.Add(new MarcaDeRegua(cm * escalaPxPorCm, maior ? RotuloDeMedida(cm) : "", maior));
        }

        for (var cm = 0.0; cm <= consumoCm + 0.01; cm += passoMenorCm)
        {
            var maior = cm % passoMaiorCm < 0.01;
            ReguaVertical.Add(new MarcaDeRegua(cm * escalaPxPorCm, maior ? RotuloDeMedida(cm) : "", maior));
        }
    }

    private static string RotuloDeMedida(double cm) => cm >= 100 ? $"{cm / 100:0.#}m" : $"{cm:0}cm";

    /// <summary>Porte de <c>desenharArte</c> (§10.1) — gira o contorno REAL da peça (não a caixa) pela rotação escolhida e desloca pra posição final, em pixels de tela.</summary>
    private static Geometry MontarGeometriaRotacionada(IReadOnlyList<PontoXY> contorno, int rotacaoGraus, double posX, double posY, double escala)
    {
        PontoXY Girar(PontoXY p) => rotacaoGraus switch
        {
            90 => new PontoXY(-p.Y, p.X),
            180 => new PontoXY(-p.X, -p.Y),
            270 => new PontoXY(p.Y, -p.X),
            _ => p,
        };

        var girados = contorno.Select(Girar).ToList();
        var minX = girados.Min(p => p.X);
        var minY = girados.Min(p => p.Y);

        var figura = new PathFigure { IsClosed = true, StartPoint = new Point((girados[0].X - minX + posX) * escala, (girados[0].Y - minY + posY) * escala) };
        foreach (var p in girados.Skip(1))
            figura.Segments!.Add(new LineSegment { Point = new Point((p.X - minX + posX) * escala, (p.Y - minY + posY) * escala) });

        return new PathGeometry { Figures = [figura] };
    }

    [ObservableProperty]
    public partial double LarguraOcupadaCm { get; set; }

    [ObservableProperty]
    public partial double SobraEsquerdaCm { get; set; }

    [ObservableProperty]
    public partial double SobraDireitaCm { get; set; }

    [ObservableProperty]
    public partial double SobraTotalCm { get; set; }

    [ObservableProperty]
    public partial double SobraCentralizadaCm { get; set; }

    [ObservableProperty]
    public partial double BarraEsquerdaPx { get; set; }

    [ObservableProperty]
    public partial double BarraOcupadaPx { get; set; }

    [ObservableProperty]
    public partial double BarraDireitaPx { get; set; }

    [ObservableProperty]
    public partial double AreaDasPecasM2 { get; set; }

    [ObservableProperty]
    public partial double SobraDeTecidoM2 { get; set; }

    [ObservableProperty]
    public partial string ResumoDoEncaixe { get; set; } = "";

    /// <summary>Porte de <c>medidasLateraisDoEncaixe</c>/<c>renderLarguraDoEncaixe</c> (§10.1) — quanto da largura do tecido as peças de fato ocupam, e quanto sobra de cada lado.</summary>
    private void MontarSobraLateral(ResultadoDeEncaixe resultado)
    {
        const double barraLarguraPx = 800;

        if (resultado.Posicoes.Count == 0 || LarguraTecidoCm <= 0)
        {
            LarguraOcupadaCm = SobraEsquerdaCm = SobraDireitaCm = SobraTotalCm = SobraCentralizadaCm = 0;
            BarraEsquerdaPx = BarraOcupadaPx = BarraDireitaPx = 0;
            AreaDasPecasM2 = SobraDeTecidoM2 = 0;
            ResumoDoEncaixe = "";
            return;
        }

        var inicio = Math.Max(0, resultado.Posicoes.Min(p => p.X));
        var fim = Math.Min(LarguraTecidoCm, resultado.Posicoes.Max(p => p.X + p.LarguraCm));
        var larguraOcupada = Math.Max(0, fim - inicio);
        var sobraEsquerda = Math.Max(0, inicio);
        var sobraDireita = Math.Max(0, LarguraTecidoCm - fim);

        LarguraOcupadaCm = larguraOcupada;
        SobraEsquerdaCm = sobraEsquerda;
        SobraDireitaCm = sobraDireita;
        SobraTotalCm = sobraEsquerda + sobraDireita;
        SobraCentralizadaCm = SobraTotalCm / 2;

        BarraEsquerdaPx = sobraEsquerda / LarguraTecidoCm * barraLarguraPx;
        BarraOcupadaPx = larguraOcupada / LarguraTecidoCm * barraLarguraPx;
        BarraDireitaPx = sobraDireita / LarguraTecidoCm * barraLarguraPx;

        MontarResumoDoEncaixe(resultado);
    }

    /// <summary>
    /// Porte de <c>comoFoiEncaixado</c>/parágrafo de <c>renderResultado</c> (§10.1) — explica em
    /// texto como o número final foi calculado. Não replica a frase original de comparar o
    /// consumo de cada motor tentado ("pelo contorno X, pela caixa Y") porque o Core hoje só
    /// guarda tentativas/vitórias por receita no placar (§12.1, <c>LinhaDoPlacar</c>), não o
    /// consumo alcançado por receita — dá pra acrescentar se isso passar a valer a pena medir.
    /// </summary>
    private void MontarResumoDoEncaixe(ResultadoDeEncaixe resultado)
    {
        var areaTecidoM2 = LarguraTecidoCm / 100.0 * resultado.ConsumoCm / 100.0;
        var areaPecasM2 = resultado.AreaRealCm2 / 10_000.0;

        AreaDasPecasM2 = areaPecasM2;
        SobraDeTecidoM2 = Math.Max(0, areaTecidoM2 - areaPecasM2);

        var folgaTexto = EspacoMm > 0 ? $" Folga entre peças: {EspacoMm:0.#} mm." : "";

        ResumoDoEncaixe =
            $"Na largura, as peças ocupam {LarguraOcupadaCm:0.0} cm dos {LarguraTecidoCm:0.0} cm do tecido; " +
            $"sobram {SobraEsquerdaCm:0.0} cm à esquerda e {SobraDireitaCm:0.0} cm à direita.{folgaTexto} " +
            $"Aproveitamento = tecido que vira peça ({areaPecasM2:0.00} m²) dividido pelo tecido gasto " +
            $"({LarguraTecidoCm:0.0} cm × {resultado.ConsumoCm / 100:0.00} m = {areaTecidoM2:0.00} m²). " +
            $"Receita vencedora: {resultado.ReceitaVencedora}.";
    }
}
