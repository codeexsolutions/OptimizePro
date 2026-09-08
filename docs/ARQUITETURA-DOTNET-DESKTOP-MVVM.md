# Optimize .NET Desktop — Arquitetura MVVM (Avalonia)

> Documento derivado de [`ESPECIFICACAO-PARA-DOTNET.md`](ESPECIFICACAO-PARA-DOTNET.md)
> (especificação técnica fiel do sistema atual em Node/Tauri). Este aqui define
> **como estruturar o projeto novo**: um aplicativo desktop nativo, instalável,
> em .NET, usando **Avalonia UI** com o padrão **MVVM**. Não é mais uma
> arquitetura cliente-servidor — o servidor HTTP, o Socket.IO e a separação
> "backend Node / frontend web" deixam de existir; tudo roda dentro do mesmo
> processo desktop.

> ## ⚠️ Atualização (01/09/2026) — Disparo/WhatsApp mudou de direção
>
> Confirmado por leitura do código-fonte atual do projeto original
> (`C:\projetos\optmize-full`): WhatsApp/Socket.IO foi **removido de lá** —
> zero menções em `server.js`, `package.json` ou qualquer outro arquivo.
> §9.5, §14 e as menções a `IWhatsAppSidecarClient`/`Optimize.WhatsAppSidecar`
> abaixo descrevem o plano **antigo** deste port (disparo de mensagens em
> massa via WhatsApp), que nunca chegou a ser implementado. Decisão com o
> usuário: o Disparo vira **captação de lead**, enviando dados pra uma base
> separada (Supabase) via notificação/webhook silencioso — sem WhatsApp em
> massa. Origem do lead e schema do Supabase ainda **não foram definidos**;
> módulo em **stand-by** (`DisparoViewModel`/`DisparoView` continuam
> placeholder). Ver §15.1 de `ESPECIFICACAO-PARA-DOTNET.md` pro registro
> completo dessa decisão. As seções abaixo ficam como referência histórica —
> não usar `IWhatsAppSidecarClient` como próximo passo sem antes revalidar se
> ainda é o plano certo.
>
> Também: `EncaixeMemoriaService` (§6.3/§12 da especificação) está confirmado
> **fiel** à fórmula de duas camadas do projeto original — não mudou. O que
> existia de novo lá (uma rede neural pequena que complementa essa memória,
> `encaixe-rede.js`) **já foi portado** (01/09/2026) — algoritmo completo e o
> mapeamento pra classes/camadas .NET documentados em §12.1 de
> `ESPECIFICACAO-PARA-DOTNET.md`.

## Índice

1. [Decisões de arquitetura e por quê](#1-decisões-de-arquitetura-e-por-quê)
2. [Stack tecnológica](#2-stack-tecnológica)
3. [Estrutura da solução (projetos)](#3-estrutura-da-solução-projetos)
4. [Camada de domínio (Optimize.Core)](#4-camada-de-domínio-optimizecore)
5. [Camada de dados (Optimize.Data)](#5-camada-de-dados-optimizedata)
6. [Camada de serviços (Optimize.Services)](#6-camada-de-serviços-optimizeservices)
7. [Camada de apresentação — MVVM (Optimize.App)](#7-camada-de-apresentação--mvvm-optimizeapp)
8. [Navegação e shell principal](#8-navegação-e-shell-principal)
9. [Tela a tela: mapeamento View/ViewModel](#9-tela-a-tela-mapeamento-viewviewmodel)
10. [Renderização customizada (encaixe, molde, vetor)](#10-renderização-customizada-encaixe-molde-vetor)
11. [Concorrência: de Web Workers para Task/Parallel](#11-concorrência-de-web-workers-para-taskparallel)
12. [Motor de Encaixe como biblioteca pura](#12-motor-de-encaixe-como-biblioteca-pura)
13. [Geração de PDF](#13-geração-de-pdf)
14. [Integração com WhatsApp](#14-integração-com-whatsapp)
15. [Armazenamento local e configuração](#15-armazenamento-local-e-configuração)
16. [Injeção de dependência e inicialização](#16-injeção-de-dependência-e-inicialização)
17. [Empacotamento e instalador](#17-empacotamento-e-instalador)
18. [Testes e validação contra o sistema atual](#18-testes-e-validação-contra-o-sistema-atual)
19. [Fases de implementação](#19-fases-de-implementação)

---

## 1. Decisões de arquitetura e por quê

| Decisão | Escolha | Motivo |
|---|---|---|
| UI framework | **Avalonia UI** (não WPF/WinUI3) | XAML+MVVM maduro, motor de renderização **Skia** nativo (mesma tecnologia que já se cogitaria via SkiaSharp) — ideal para as telas de desenho pesado (encaixe, vetor, molde). Também abre a porta a versão Linux/macOS no futuro, sem reescrever a UI. |
| Padrão de UI | **MVVM** com `CommunityToolkit.Mvvm` | Reduz boilerplate (`[ObservableProperty]`, `[RelayCommand]`), é o padrão de fato do ecossistema Avalonia/WPF/WinUI hoje. |
| Arquitetura geral | **Monólito desktop em camadas**, sem servidor HTTP interno | O sistema atual só usa Express/Socket.IO porque a UI é uma página web dentro do Tauri. Numa app MVVM nativa isso é desnecessário: ViewModels chamam Services diretamente (in-process), e progresso assíncrono é reportado via `IProgress<T>`/eventos, não via broadcast de rede. |
| Persistência | **SQLite** via `Microsoft.EntityFrameworkCore.Sqlite` | Mesma engine de hoje, mapeamento direto do schema já documentado. |
| Motor de encaixe | Porta 1:1 para **C# puro** (Core, sem UI) | O documento anterior já recomenda isso (§11.10) — não há WebAssembly nem fronteira JS↔nativo em um app .NET; a mesma lógica escrita em C# com `Span<T>` já entrega a performance que o WASM buscava. |
| Concorrência | `Task.Run`/`Parallel.For`/`System.Threading.Channels` | Substitui Web Workers 1:1 conceitualmente (fatias de busca em paralelo), mas mais simples: memória compartilhada direta, sem serialização de mensagens. |
| Geração de PDF | `QuestPDF` | API moderna, licença gratuita para uso não-comercial/pequenas empresas (conferir enquadramento), boa performance; ver §13 para a ressalva do `/UserUnit`. |
| Empacotamento/instalador | **Velopack** | É o empacotador padrão do ecossistema Avalonia hoje (sucessor do Squirrel.Windows), gera instalador `.exe`/MSIX, suporta auto-update, self-contained. Alternativa mais simples: Inno Setup sobre um `dotnet publish` self-contained. |
| WhatsApp | Processo Node lateral isolado (ver §14) | Não existe biblioteca .NET madura equivalente a `whatsapp-web.js`; a opção mais realista de curto prazo é manter um micro-processo Node dedicado só a isso, comunicando com o app via IPC local (pipe nomeado ou HTTP em `localhost` restrito ao loopback). |

---

## 2. Stack tecnológica

| Camada | Biblioteca / Tecnologia |
|---|---|
| UI Framework | Avalonia UI 11+ |
| MVVM toolkit | CommunityToolkit.Mvvm |
| Injeção de dependência | `Microsoft.Extensions.DependencyInjection` + `Microsoft.Extensions.Hosting` (Generic Host) |
| ORM / persistência | `Microsoft.EntityFrameworkCore.Sqlite` |
| Renderização customizada | Avalonia `Custom Draw` (`DrawingContext`) — motor nativo já é Skia, sem necessidade de `SkiaSharp` separado para 2D básico; usar `SkiaSharp` diretamente só se precisar de APIs Skia avançadas fora do que o `DrawingContext` do Avalonia expõe |
| Geração de PDF | QuestPDF |
| Geometria/imagem | `System.Numerics` (Vector2), `SixLabors.ImageSharp` (decodificação/leitura de DPI de PNG/JPEG) |
| Logging | `Microsoft.Extensions.Logging` + Serilog (arquivo local) |
| Empacotamento | Velopack |
| Testes | xUnit + FluentAssertions (+ testes de regressão numérica do encaixe/vetor) |
| WhatsApp (sidecar) | Node.js + `whatsapp-web.js` num processo filho, IPC via named pipe/HTTP loopback |

---

## 3. Estrutura da solução (projetos)

```
Optimize.sln
├── src/
│   ├── Optimize.Core/              # domínio puro: modelos, geometria, motor de encaixe, vetorização
│   │   ├── Geometria/
│   │   ├── Moldes/                 # parsers DXF/PLT/SVG/PDF
│   │   ├── Encaixe/                # motor de nesting (o núcleo)
│   │   ├── Vetor/                  # raster → SVG
│   │   └── Modelos/                # entidades de domínio (POCOs, sem EF)
│   │
│   ├── Optimize.Data/              # EF Core: DbContext, entidades mapeadas, migrations, repositórios
│   │   ├── OptimizeDbContext.cs
│   │   ├── Entidades/
│   │   ├── Migrations/
│   │   └── Repositorios/
│   │
│   ├── Optimize.Services/          # orquestração: casos de uso, sem UI, sem EF diretamente exposto
│   │   ├── MoldeService.cs
│   │   ├── ProjetoService.cs
│   │   ├── EncaixeService.cs
│   │   ├── VetorService.cs
│   │   ├── DisparoService.cs
│   │   ├── ArquivoService.cs        # upload/faxina de disco (equivalente a uploads-arquivos.js)
│   │   └── WhatsAppSidecarClient.cs # fala com o processo Node lateral
│   │
│   ├── Optimize.App/               # Avalonia: Views, ViewModels, App.axaml, shell
│   │   ├── App.axaml(.cs)
│   │   ├── ViewModels/
│   │   ├── Views/
│   │   ├── Controls/                # controles customizados (canvas de encaixe, prévia de vetor...)
│   │   ├── Converters/
│   │   └── Assets/
│   │
│   └── Optimize.WhatsAppSidecar/   # projeto Node.js separado (não é .NET), empacotado junto no instalador
│       ├── package.json
│       └── sidecar.js
│
├── tests/
│   ├── Optimize.Core.Tests/        # regressão numérica de geometria, encaixe, vetor
│   ├── Optimize.Services.Tests/
│   └── Optimize.Data.Tests/
│
└── installer/
    └── velopack.config / Inno Setup script
```

**Regra de dependência** (equivalente ao "dependência só aponta para baixo" já
documentado em `docs/MAPA.md` do projeto original):

```
Optimize.App  →  Optimize.Services  →  Optimize.Data
                        ↓                    ↓
                  Optimize.Core  ←───────────┘
```

`Optimize.Core` não referencia `Optimize.Data` nem `Optimize.App` — é a
biblioteca "sem saber do mundo" (mesmo princípio de `geometria.js`/`encaixe-motor.js`
no sistema atual, que rodam até dentro de Web Worker sem `document`/`window`).
Isso também é o que torna `Optimize.Core` **testável isoladamente** e
reutilizável se um dia se quiser expor a mesma lógica de encaixe como serviço
de linha de comando ou biblioteca separada.

---

## 4. Camada de domínio (Optimize.Core)

Contém **toda a lógica de negócio pura**, sem nenhuma dependência de Avalonia,
EF Core ou I/O de disco/rede. É o equivalente direto de `geometria.js`,
`moldes.js`, `encaixe-motor.js`, `encaixe-mascara.js`, `nfp.js`,
`encaixe-giro.js`, `vetor.js`, `arte-molde.js` no sistema atual.

### 4.1 `Optimize.Core.Geometria`

```csharp
public static class Geometria
{
    public static double AreaComSinal(IReadOnlyList<PontoXY> pontos);
    public static CaixaXY CaixaDeContorno(IReadOnlyList<PontoXY> pontos);
    public static double LadoMenorDoContorno(IReadOnlyList<PontoXY> pontos);
    public static double DistanciaEntre(PontoXY a, PontoXY b);
    public static double DistanciaAteSegmento(PontoXY p, PontoXY a, PontoXY b);
    public static IReadOnlyList<PontoXY> Simplificar(IReadOnlyList<PontoXY> pontos, double tolerancia); // Douglas-Peucker iterativo
}

public readonly record struct PontoXY(double X, double Y);
public readonly record struct CaixaXY(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Largura => MaxX - MinX;
    public double Altura => MaxY - MinY;
}
```

Porte **linha a linha** de `geometria.js` (§8.2 do documento de especificação)
— são as funções mais simples e mais reaproveitadas do sistema; começar por
aqui e cobrir com testes unitários antes de tocar em qualquer outra coisa.

### 4.2 `Optimize.Core.Moldes`

Um leitor por formato, todos implementando uma interface comum:

```csharp
public interface ILeitorDeMolde
{
    bool SuportaExtensao(string extensao);
    Task<ResultadoLeituraMolde> LerAsync(Stream conteudo, OpcoesLeituraMolde opcoes);
}

public sealed record OpcoesLeituraMolde(string? UnidadeForcada, ModoLeituraVetor Modo); // Modo: Marcador | Inteiro

public sealed record ResultadoLeituraMolde(
    IReadOnlyList<PecaLida> Pecas,
    string Unidade,
    IReadOnlyList<string> Avisos,
    string? Erro);

public sealed record PecaLida(string Nome, IReadOnlyList<PontoXY> Contorno,
    IReadOnlyList<IReadOnlyList<PontoXY>> Furos, double LarguraCm, double AlturaCm);
```

Implementações: `LeitorDxf`, `LeitorPlt`, `LeitorSvg`, `LeitorPdf` — cada uma
portando o algoritmo já documentado em §8.1 da especificação (bulge→arco,
De Boor para SPLINE, decodificação PE do HP-GL, marching-squares do modo
"arte inteira", parser de content-stream do PDF, etc.).

**Atenção especial ao SVG**: o parser atual delega ao `DOMParser`/`getPointAtLength`
do navegador — não existe equivalente pronto fora de um browser. Duas opções:

1. Implementar path-flattening manual (Bézier cúbica/quadrática, arco elíptico
   SVG) — replicável com as fórmulas já documentadas em §8.1 (a mesma
   matemática de `comandoDeArco`/`centroDoArco` do módulo Vetor serve de
   referência para o arco elíptico do SVG).
2. Usar uma lib de terceiros com suporte a path SVG (ex.: `Svg.Skia`, que já
   converte path SVG para geometria Skia — compatível com o pipeline de
   renderização do Avalonia) e extrair os pontos amostrados a partir da
   geometria resultante.

A opção 2 reduz risco de porte; recomenda-se validar `Svg.Skia` num spike antes
de decidir.

Serviço agregador (equivalente a `lerMoldeVetorial`):

```csharp
public sealed class LeitorDeMoldeVetorial
{
    private readonly IEnumerable<ILeitorDeMolde> _leitores;
    public Task<ResultadoLeituraMolde> LerAsync(string caminhoArquivo, OpcoesLeituraMolde opcoes);
}
```

Registrado via DI com todas as implementações injetadas (`IEnumerable<ILeitorDeMolde>`).

### 4.3 `Optimize.Core.Arte`

Porte de `arte-molde.js` (§9.1) — cálculo puro de posicionamento/tamanho da
arte dentro do contorno (`AjusteArte`, `EncaixeDaArte`, `TamanhoDoRapport`).
**Sem** desenhar nada aqui — só devolve as coordenadas/dimensões calculadas; o
desenho de fato (canvas) fica na camada de apresentação (§10), consumindo esse
resultado.

```csharp
public enum TipoArte { Arte, Rapport }
public enum ModoEncaixeArte { Cobrir, Caber, Esticar }

public sealed record AjusteArte(TipoArte Tipo, ModoEncaixeArte Modo, double EscalaPercentual,
    double DeslocamentoXCm, double DeslocamentoYCm, int GirauGraus, double? PpcmArquivo);

public static class ArteMolde
{
    public static RetanguloArte EncaixeDaArte(double arteW, double arteH, double alvoW, double alvoH, AjusteArte ajuste);
    public static TamanhoRapport TamanhoDoRapport(double arteW, double arteH, AjusteArte ajuste);
    public static double PpcmDaArte(double larguraCm, double alturaCm, double dpi); // teto de 26 megapixels/peça
}
```

### 4.4 `Optimize.Core.Encaixe`

O núcleo mais valioso do sistema — porte completo de `encaixe-motor.js`,
`encaixe-mascara.js`, `nfp.js`, `encaixe-giro.js` e da lógica que hoje está em
`wasm/src/lib.rs`. Ver detalhamento dedicado em §12.

### 4.5 `Optimize.Core.Vetor`

Porte de `vetor.js` (§14 da especificação): quantização por median cut,
limpeza de ruído, extração de contorno, detecção de círculo/elipse,
simplificação, remontagem reta/arco/curva, subpixel. Toda a lógica é
"processamento de array de pixels → geometria" — não depende de canvas; a
decodificação/reamostragem da imagem de entrada usa `SixLabors.ImageSharp` na
camada de serviço (§6), e este projeto só recebe `byte[]`/`Span<byte>` RGBA já
prontos.

```csharp
public static class Vetorizador
{
    public static ResultadoVetorizacao Vetorizar(ImagemRgba dados, OpcoesVetorizacao opcoes);
}
```

---

## 5. Camada de dados (Optimize.Data)

### 5.1 Entidades EF Core (mapeamento 1:1 do schema documentado em §3 da especificação)

```csharp
public class Molde { public int Id; public string Nome; public string? Observacoes;
    public DateTime CriadoEm; public DateTime? AtualizadoEm;
    public List<MoldePeca> Pecas; public List<MoldeArte> Artes; }

public class MoldePeca { public int Id; public int MoldeId; public string Tamanho;
    public string Papel; public string? Nome; public int Quantidade;
    public double Largura; public double Altura;
    public string ContornoJson; public string? FurosJson;   // ver 5.2
    public string? Origem; public int Ordem; }

public class MoldeArte { public int Id; public int MoldeId; public string Nome;
    public DateTime CriadoEm; public DateTime? AtualizadoEm; public List<MoldeArtePeca> Pecas; }

public class MoldeArtePeca { public int Id; public int ArteId; public string Papel;
    public string Arquivo; public string? NomeOriginal; public string AjusteJson; }

// ProjetoCliente — NUNCA renomear para "Cliente"/tabela "clientes":
// instalações antigas do sistema original têm uma tabela legada `clientes`
// (módulo comercial removido) com schema incompatível.
public class ProjetoCliente { public int Id; public string Nome; public string? Observacoes;
    public DateTime CriadoEm; public DateTime? AtualizadoEm; public List<Projeto> Projetos; }

public class Projeto { public int Id; public int ClienteId; public string Nome; public string? Observacoes;
    public double? LarguraTecido; public double? Espaco; public double? Margem; public string? Giro;
    public DateTime CriadoEm; public DateTime? AtualizadoEm; public List<ProjetoPeca> Pecas; }

public class ProjetoPeca { public int Id; public int ProjetoId; public string Nome; public string Arquivo;
    public double Largura; public double Altura; public int Quantidade; public int Ordem; public string? Miniatura; }

public class EncaixeReceita { public int Id; public string Assinatura; public string Receita;
    public int Usos; public int Vitorias; public DateTime AtualizadoEm; }

public class EncaixeGuardado { public string Chave; public string? Assinatura;
    public double? LarguraTecido; public double? Espaco; public double? Margem;
    public double Consumo; public double? Aproveitamento;
    public string? PecasJson; public string PosicoesJson; public string? Receita;
    public DateTime CriadoEm; public DateTime? AtualizadoEm; }

public class EncaixeHistorico { public int Id; public string Assinatura; public double? LarguraTecido;
    public int? Pecas; public double? Consumo; public double? Aproveitamento; public string? Receita;
    public int? Tentativas; public DateTime CriadoEm; }
```

### 5.2 Campos JSON

Os mesmos campos que hoje são TEXT com JSON serializado manualmente
(`contorno`, `furos`, `ajuste`, `posicoes`, `pecas` em `EncaixeGuardado`)
seguem como colunas `TEXT` — mas em vez de o código de serviço fazer
`JsonSerializer.Serialize/Deserialize` toda vez manualmente, usar **EF Core
Value Converters** para mapear direto para os tipos ricos:

```csharp
modelBuilder.Entity<MoldePeca>()
    .Property(p => p.Contorno)
    .HasConversion(
        v => JsonSerializer.Serialize(v, default),
        v => JsonSerializer.Deserialize<List<PontoXY>>(v, default)!)
    .HasColumnName("contorno");
```

Isso deixa as entidades expostas aos Services já com `List<PontoXY>` tipado,
sem cada Service reimplementar a (de)serialização — um ganho sobre o código
JS original, onde cada rota fazia `JSON.parse`/`JSON.stringify` manualmente.

### 5.3 `OptimizeDbContext`

```csharp
public class OptimizeDbContext : DbContext
{
    public DbSet<Molde> Moldes => Set<Molde>();
    public DbSet<MoldePeca> MoldePecas => Set<MoldePeca>();
    public DbSet<MoldeArte> MoldeArtes => Set<MoldeArte>();
    public DbSet<MoldeArtePeca> MoldeArtePecas => Set<MoldeArtePeca>();
    public DbSet<ProjetoCliente> ProjetoClientes => Set<ProjetoCliente>();
    public DbSet<Projeto> Projetos => Set<Projeto>();
    public DbSet<ProjetoPeca> ProjetoPecas => Set<ProjetoPeca>();
    public DbSet<EncaixeReceita> EncaixeReceitas => Set<EncaixeReceita>();
    public DbSet<EncaixeGuardado> EncaixeGuardados => Set<EncaixeGuardado>();
    public DbSet<EncaixeHistorico> EncaixeHistoricos => Set<EncaixeHistorico>();
}
```

Cascades (`ON DELETE CASCADE`) configurados via Fluent API, espelhando as FKs
documentadas em §3. Migração inicial gerada via `dotnet ef migrations add
InicialOptimize` cobre exatamente o schema já existente — **não recriar as
tabelas legadas** (`clientes`, `produtos`, etc.) de forma alguma; se um banco
antigo for importado, tratar como migração de dados assistida, fora do
DbContext principal.

### 5.4 Repositórios

Repositórios finos (não Generic Repository genérico demais) — um por
agregado, expondo só as operações que os Services realmente precisam
(`IMoldeRepository`, `IProjetoRepository`, `IEncaixeMemoriaRepository`). Isso
espelha diretamente as rotas hoje existentes em `moldes-api.js`/`projetos-api.js`/
`encaixe-memoria.js` (§4 da especificação) — cada método do repositório
corresponde a uma rota antiga, só que chamado in-process.

---

## 6. Camada de serviços (Optimize.Services)

Orquestra `Optimize.Core` + `Optimize.Data` + I/O de arquivo. É a camada que
os ViewModels chamam diretamente — não existe mais "rota HTTP" nem "socket
event"; existe **método de serviço assíncrono**, com progresso reportado via
`IProgress<T>` ou eventos `event EventHandler<...>`.

### 6.1 `MoldeService`

Substitui `moldes-api.js` (§4.2). Métodos espelhando as rotas antigas:

```csharp
public interface IMoldeService
{
    Task<IReadOnlyList<MoldeResumo>> ListarAsync();
    Task<MoldeDetalhado> ObterAsync(int id);
    Task<int> CriarAsync(MoldeEntrada entrada);
    Task AtualizarAsync(int id, MoldeEntrada entrada);
    Task ExcluirAsync(int id);
    Task<string> SalvarImagemDeArteAsync(int moldeId, string papel, byte[] bytes, string? contentType);
    Task<IReadOnlyList<Estampa>> ListarEstampasAsync(int moldeId);
    Task<int> SalvarEstampaAsync(int moldeId, EstampaEntrada entrada);
    Task ExcluirEstampaAsync(int moldeId, int arteId);
}
```

A validação de peça (`arrumarPeca`, contorno ≥3 pontos, `quantidade` mínimo 1,
etc. — §4.2) e de ajuste de arte (`arrumarAjuste` — §8.4) migra para aqui como
validação de domínio, não mais "validação de payload HTTP" — usar
`FluentValidation` ou validação manual simples, o importante é preservar as
mesmas regras documentadas.

### 6.2 `ProjetoService`

Substitui `projetos-api.js` (§4.3): CRUD de cliente/projeto/peça, incluindo a
regra de miniatura (`PatchMiniaturasAsync`, gravando só a coluna miniatura sem
tocar no resto — §4.3).

### 6.3 `EncaixeService`

A peça mais crítica. Orquestra o motor puro (`Optimize.Core.Encaixe`) mais a
integração com a memória (`EncaixeReceita`/`EncaixeGuardado`/`EncaixeHistorico`)
e com a geração de PDF.

```csharp
public interface IEncaixeService
{
    Task<ResultadoEncaixe> BuscarMelhorEncaixeAsync(
        IReadOnlyList<ItemEncaixe> itens,
        ConfiguracaoEncaixe config,
        IProgress<AndamentoEncaixe> progresso,
        CancellationToken cancelamento);

    Task<MemoriaDoTipo> ConsultarMemoriaAsync(string assinatura);
    Task RegistrarResultadoAsync(RegistroDeEncaixe registro);
    Task<EncaixeGuardadoDto?> BuscarGuardadoAsync(string chave);
    Task<bool> GuardarSeMelhorAsync(EncaixeGuardadoDto guardado);
    Task LimparMemoriaAsync();
}
```

O `CancellationToken` substitui o evento `stop-dispatch`/"parar e usar este" —
a UI cancela o token e o serviço devolve o melhor resultado já encontrado até
ali (mesma semântica de "usar o melhor até agora", só que via cancelamento
cooperativo em vez de uma flag checada manualmente).

`IProgress<AndamentoEncaixe>` substitui a emissão de `log`/andamento via
Socket.IO — o ViewModel se inscreve e atualiza a UI (contagem de tentativas,
tempo decorrido, melhor resultado até agora), exatamente como a linha abaixo
do botão "Fazer encaixe" faz hoje.

### 6.4 `VetorService`

Envolve `Optimize.Core.Vetor`: decodifica a imagem de entrada (via
`ImageSharp`), reamostra para 1800px de lado maior (§14.1), chama
`Vetorizador.Vetorizar`, monta a string SVG final. Roda em `Task.Run` (§11).

### 6.5 `ArquivoService`

Substitui `uploads-arquivos.js` (§6): detecção de tipo por assinatura binária,
geração de nome sem colisão, e a **faxina de arquivos órfãos com a mesma regra
crítica** — conferir contra a tabela inteira, nunca só contra o que acabou de
mudar.

```csharp
public interface IArquivoService
{
    string? DetectarExtensao(ReadOnlySpan<byte> bytes);
    string GerarNomeSemColisao(string prefixo, string extensao);
    Task<string> SalvarAsync(string pasta, string nomeArquivo, byte[] bytes);
    Task LimparOrfaosAsync(string pasta, IEnumerable<string> arquivosEmUso, IEnumerable<string> candidatosARemover);
}
```

### 6.6 `DisparoService`

Orquestra o fluxo de disparo (normalização de número, template `{{nome}}`,
delay aleatório, log CSV) e fala com o sidecar do WhatsApp (§14) através de
`IWhatsAppSidecarClient`. Progresso via `IProgress<ProgressoDisparo>`
(equivalente a `dispatch-progress`); suporte a cancelamento (equivalente a
`stop-dispatch`).

---

## 7. Camada de apresentação — MVVM (Optimize.App)

### 7.1 Convenções

- **ViewModels** herdam de `ViewModelBase : ObservableObject` (CommunityToolkit.Mvvm).
- Propriedades observáveis via `[ObservableProperty]`; comandos via
  `[RelayCommand]` (com suporte a `CanExecute` e versões `Async` com
  cancelamento nativo do toolkit).
- **Views** são `UserControl`/`Window` em Avalonia XAML (`.axaml`), sem
  code-behind com lógica de negócio — só o mínimo de glue (ex.: hookar um
  `Canvas`/controle customizado que precise de referência direta).
- Resolução View↔ViewModel por convenção de nome (`FooViewModel` →
  `FooView`), via um `ViewLocator` central registrado no `App.axaml.cs`
  (padrão comum em templates Avalonia MVVM).
- Nenhum ViewModel referencia tipos do Avalonia diretamente (sem `Bitmap`,
  sem `Control`) — usa tipos próprios (`ImagemPreview`, `PontoTela` etc.) que
  as Views convertem via `IValueConverter`. Mantém os ViewModels testáveis sem
  precisar inicializar o runtime gráfico.

### 7.2 Estrutura de pastas em `Optimize.App`

```
ViewModels/
  MainWindowViewModel.cs        # shell: menu lateral, navegação, status de conexão WhatsApp
  MoldesViewModel.cs
  MoldeWizardViewModel.cs        # os 3 passos de criação de molde
  MoldeArteEnvioViewModel.cs     # "arte e encaixe" — estampas, rapport
  ProjetosViewModel.cs
  ProjetoEditorViewModel.cs
  EncaixeViewModel.cs
  VetorViewModel.cs
  DisparoViewModel.cs
  ConfiguracoesViewModel.cs

Views/
  MainWindow.axaml
  MoldesView.axaml
  MoldeWizardView.axaml
  MoldeArteEnvioView.axaml
  ProjetosView.axaml
  ProjetoEditorView.axaml
  EncaixeView.axaml
  VetorView.axaml
  DisparoView.axaml
  ConfiguracoesView.axaml

Controls/
  CanvasDeEncaixeControl.cs      # desenho customizado do rolo de tecido
  PreviaVetorControl.cs           # duas prévias lado a lado, com lupa/zoom
  MiniaturaDeContornoControl.cs   # miniatura de peça de molde
```

---

## 8. Navegação e shell principal

O sistema atual tem seis áreas em duas seções ("Produção": Moldes, Projetos,
Encaixe, Vetor; "Comunicação": Disparo, Configurações), sem login, entrando
direto em Moldes.

`MainWindowViewModel` mantém:

```csharp
public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _telaAtual;
    [ObservableProperty] private string _statusWhatsApp; // "desconectado" | "qr" | "pronto" | ...

    public IReadOnlyList<ItemDeMenu> ItensDeMenu { get; } = /* Produção: Moldes/Projetos/Encaixe/Vetor, Comunicação: Disparo/Configurações */;

    [RelayCommand]
    private void NavegarPara(ItemDeMenu item) => TelaAtual = _fabricaDeViewModels.Criar(item.Tipo);
}
```

Uma `DataTemplate`/`ViewLocator` no `MainWindow.axaml` troca o conteúdo
central conforme `TelaAtual` — sem `Frame`/roteamento de URL, é troca direta
de instância de ViewModel (mais simples que o `app.js`/`interface.js` atuais,
que manipulam `display:none`/`display:block` de seções de uma página só).

Cada tela mantém seu próprio ViewModel vivo (ou recriado, a decidir por
tela — ex.: manter `EncaixeViewModel` vivo ao trocar de aba preserva o
resultado calculado, replicando o comportamento atual onde trocar de aba não
perde o encaixe montado, só um F5 perde).

---

## 9. Tela a tela: mapeamento View/ViewModel

### 9.1 Moldes (`MoldesViewModel` + `MoldeWizardViewModel` + `MoldeArteEnvioViewModel`)

Mapeamento direto de `moldes-tela.js` (§8.3–9 da especificação):

- `MoldesViewModel`: lista de moldes (`ObservableCollection<MoldeResumo>`),
  comandos `Encaixar`/`Editar`/`Excluir` por linha, comando `AdicionarNovo`
  que abre o wizard.
- `MoldeWizardViewModel`: estado dos 3 passos (`PassoAtual`,
  `TipoEscolhido`, `PartesPorTamanho: Dictionary<string, ObservableCollection<ParteViewModel>>`,
  `TamanhoAberto`). Cada `ParteViewModel` expõe `Papel`, `Quantidade`,
  `Contorno` (já lido), `Miniatura` (gerada via `Optimize.Core.Moldes` +
  renderizado com o `MiniaturaDeContornoControl`). Comando
  `EnviarArquivoParaParte` chama `LeitorDeMoldeVetorial.LerAsync` e aplica a
  mesma lógica de distribuição em vagas vazias (§8.3).
- `MoldeArteEnvioViewModel`: estado de `ArtesPorPapel`, estampas carregadas/
  salvas, cálculo de DPI seguro, comando `MandarParaEncaixe` que monta as
  peças finais (chamando `ArteMolde.EncaixeDaArte`/`TamanhoDoRapport` e
  desenhando via `Optimize.Core` + o controle de renderização) e navega para
  `EncaixeViewModel` com as peças já carregadas.

### 9.2 Projetos (`ProjetosViewModel` + `ProjetoEditorViewModel`)

Mapeamento de `projetos.js` (§10): navegação em duas colunas
(Clientes → Projetos do cliente), editor de projeto com lista de peças,
campo "unidades" com cálculo reativo (`porUnidade * unidades`, via
`[ObservableProperty]` + recomputação automática), comando
`MandarParaEncaixe` que copia os 4 ajustes (`LarguraTecido`, `Espaco`,
`Margem`, `Giro`) para o `EncaixeViewModel` de destino e dispara a remoção de
fundo em lote antes de navegar.

### 9.3 Encaixe (`EncaixeViewModel`)

A tela mais complexa. Estado:

```csharp
public partial class EncaixeViewModel : ViewModelBase
{
    public ObservableCollection<PecaEncaixeViewModel> Pecas { get; } = new();
    [ObservableProperty] private double _larguraTecidoCm;
    [ObservableProperty] private double _espacoMm;
    [ObservableProperty] private double _margemCm;
    [ObservableProperty] private ModoComoEncaixar _comoEncaixar; // Automatico | SemprePeloContorno | SemprePelaCaixa
    [ObservableProperty] private ResultadoEncaixe? _resultado;
    [ObservableProperty] private AndamentoEncaixe? _andamento;    // tentativa, tempo, melhor até agora
    [ObservableProperty] private bool _emBusca;

    [RelayCommand] private async Task FazerEncaixeAsync(CancellationToken ct) { /* chama IEncaixeService */ }
    [RelayCommand] private void PararEUsarEste() => _cts.Cancel();
    [RelayCommand] private async Task BaixarPdfAsync();
    [RelayCommand] private async Task ExportarPngAsync();
}
```

O `CanvasDeEncaixeControl` (§10) recebe `Resultado` via binding e desenha o
rolo com régua lateral, contorno de cada peça, legenda — porte direto de
`desenharEncaixe` (§11 da especificação, seção "Geração do desenho final").

### 9.4 Vetor (`VetorViewModel`)

Estado dos parâmetros (`Cores`, `Detalhe`, `Suavidade`, `Quina`, `Tensao`,
`Redondas`, `PorTrechos`, `Subpixel`, `JuntarSombras`), atalhos pré-configurados
como comandos que setam múltiplas propriedades de uma vez, e as duas prévias
lado a lado com zoom/pan sincronizado (`PreviaVetorControl`, §10). Toda
mudança de parâmetro reexecuta `VetorService.Vetorizar` em `Task.Run`
(debounce leve para não recalcular a cada tecla digitada num slider, se
aplicável).

### 9.5 Disparo (`DisparoViewModel`) e Configurações (`ConfiguracoesViewModel`)

> **Plano antigo, superado — ver aviso no topo do documento.** Disparo virou
> captação de lead → Supabase, ainda em stand-by. Descrição abaixo preservada
> como referência histórica.

Lista de contatos (colada ou importada de CSV — usar um parser CSV real, ex.
`CsvHelper`, corrigindo a limitação do parser atual que trata CSV como texto
linha a linha sem escapar aspas/vírgulas), campo de mensagem com
`{{nome}}`, anexo de mídia, progresso do disparo (`ObservableCollection` de
resultados por contato), comandos `IniciarDisparo`/`Parar`. Configurações:
código do país/DDD padrão, intervalo entre envios, conectar/desconectar
WhatsApp — este último decidido pelo status reportado pelo sidecar (§14).

---

## 10. Renderização customizada (encaixe, molde, vetor)

Três telas precisam de desenho vetorial customizado equivalente ao que hoje é
feito em `<canvas>` 2D. Como o motor de renderização do Avalonia já é Skia,
duas abordagens são válidas — **recomenda-se a primeira**:

1. **`Control` customizado com `Render(DrawingContext)` do próprio Avalonia**
   — usa a API de desenho nativa (`context.DrawGeometry`, `context.DrawImage`,
   `StreamGeometry` para paths complexos com curvas/arcos). Suficiente para
   tudo que as três telas precisam (retângulos, paths com Bézier/arco,
   imagens bitmap, texto). Sem dependência extra.
2. **`SkiaSharp` embutido via `ICustomDrawOperation`** — só se algo específico
   do Avalonia `DrawingContext` não cobrir (ex.: algum blend mode ou efeito
   avançado). Não é esperado ser necessário para replicar o que o sistema
   atual faz.

### 10.1 `CanvasDeEncaixeControl`

Porta `desenharEncaixe` (§11, "Geração do desenho final"): escala
px/cm calculada para caber na largura disponível, régua lateral, `Clip` pelo
retângulo de cada posição + desenho da arte já rotacionada (`PushTransform`
equivalente ao `translate`+`rotate` do canvas), contorno via `StreamGeometry`
construído a partir das faixas pré-computadas (mesma otimização de
"faixas horizontais cacheadas" do código atual, §11 "12.1").

### 10.2 `PreviaVetorControl`

Duas prévias (original vs. SVG gerado) com zoom/pan sincronizado — usar
`RenderTransform` (`ScaleTransform`+`TranslateTransform`) compartilhado entre
os dois `Control`s via binding a um `ZoomPanState` comum no ViewModel, e
`PointerWheelChanged`/`PointerPressed`+`PointerMoved` para capturar
roda-do-mouse e arraste (equivalente ao "roda aproxima no ponto do ponteiro,
arrastar move" do sistema atual, §14.1).

### 10.3 `MiniaturaDeContornoControl`

Desenha só o contorno simplificado de uma peça (usado nas listas de Moldes) —
leve o suficiente para renderizar dezenas de linhas de lista sem custo, ao
contrário de decodificar a arte de impressão inteira (mesma regra "miniatura
nunca é a arte inteira" do §17.3 da especificação).

---

## 11. Concorrência: de Web Workers para Task/Parallel

Mapeamento direto das responsabilidades hoje divididas entre `encaixe-worker.js`/
`prepara-worker.js`/`vetor-worker.js` (§11.9 e §14.8 da especificação):

| Hoje (Web Worker) | Em .NET |
|---|---|
| `poolEncaixe` — N workers, cada um com uma fatia de receitas | `Parallel.For(0, n, ...)` ou `Task.WhenAll(Enumerable.Range(0,n).Select(k => Task.Run(...)))`, cada tarefa roda `BuscarMelhorEncaixe` com sua fatia `(k, n)` |
| `postMessage` com `andamento`/`resultado` | `IProgress<T>` (thread-safe por design) por tarefa, agregado no ViewModel; resultado final via `Task<ResultadoEncaixe>` de cada tarefa, combinado com `MelhorQue` (§11.7) |
| `poolPrepara` — preparo de máscaras em paralelo | mesma ideia, `Task.Run` por peça ou `Parallel.ForEach` |
| `vetor-worker.js` — 1 vetorização por vez, fora da UI thread | `Task.Run(() => Vetorizador.Vetorizar(...))` |
| Cancelamento via flag `deveParar()` checada periodicamente | `CancellationToken` propagado a todas as tarefas — mais idiomático, cooperativo, integrado ao `RelayCommand` async do toolkit |

**Importante**: como não há mais fronteira de serialização (Web Worker →
`postMessage` → estrutura clonada), a implementação .NET pode **compartilhar
memória diretamente** entre as tarefas paralelas de busca (com os devidos
cuidados de imutabilidade/cópia onde o algoritmo espera estado isolado por
tentativa — ex.: cada tarefa de busca precisa de seu próprio `int[] perfil`,
não compartilhado entre fatias, exatamente como hoje cada worker tem seu
`perfil` próprio).

A UI (thread principal do Avalonia) nunca deve bloquear — todo `IProgress<T>.Report`
já faz o marshalling de volta à UI thread automaticamente (comportamento
padrão de `Progress<T>`), preservando a mesma responsividade que hoje vem de
rodar tudo fora da thread da página.

---

## 12. Motor de Encaixe como biblioteca pura

Esta seção existe para reforçar, no contexto MVVM, o que já está detalhado
em §11 da especificação técnica — aqui o foco é **onde cada peça mora** na
nova solução, não o algoritmo em si (que já está descrito lá com pseudocódigo
completo).

```
Optimize.Core.Encaixe/
  Modelos/
    Mascara.cs           # topo/base/desenho/cheio/offX/offY
    Forma.cs              # cols/rows/topo/base/partes/nCols/somaTopo/maxBase
    ItemEncaixe.cs
    ResultadoEncaixe.cs
    Receita.cs
  Encaixadores/
    IEncaixador.cs
    EncaixadorPorContorno.cs   # melhorPosicaoDaUnidade / encaixarContorno — o núcleo
    EncaixadorPorCaixa.cs      # MaxRects
    EncaixadorPorFaixas.cs
    EncaixadorPorNfp.cs
  Nfp/
    DecomposicaoConvexa.cs     # Hertel-Mehlhorn
    SomaDeMinkowski.cs
    IndiceEspacialNfp.cs
  Busca/
    BuscaDeReceitas.cs          # buscarMelhorEncaixe: passada base, poda, parede, perseguição
    Poda.cs
    Agrupamento.cs               # formasDoBloco, encostarNaForma
  Paralelizacao/
    ExecutorParalelo.cs          # substitui encaixe-paralelo.js
  Mascaramento/
    SilhuetaDeDados.cs            # remoção de fundo, engorde pela folga
```

**Não há módulo WASM separado** — o que hoje é `wasm/src/lib.rs` +
`encaixe-wasm.js` (interface JS↔Rust, layout de memória manual, bump
allocator) deixa de existir como conceito: é só a implementação C# de
`EncaixadorPorContorno`, otimizada com `Span<int>`/arrays reutilizados entre
chamadas (evitando alocação por tentativa, que é o mesmo motivo de
performance que levou ao WASM original). Se após medição a performance em C#
puro não bastar, as opções de escalonamento (nessa ordem de preferência)
seriam: (a) otimizar alocação/algoritmo em C# gerenciado, (b) `unsafe`/
`stackalloc` para os arrays mais quentes, (c) só em último caso, um módulo
nativo via P/Invoke — não deveria ser necessário dado que o gargalo original
era a fronteira JS↔WASM, que simplesmente não existe aqui.

**Regressão numérica**: o motor de encaixe tem números de referência muito
bem documentados no `README.md` original (ex.: "camiseta+manga+gola: 7,140m
em 24s de NFP", "trio: 5,763→5,540m", tabelas completas de ganho por
otimização). Usar esses mesmos casos de teste como suíte de regressão em
`Optimize.Core.Tests` — a qualidade do porte se mede batendo (ou superando) os
números já documentados, não achando "parece razoável".

---

## 13. Geração de PDF

Porte de `encaixe-pdf.js` (§13 da especificação) usando **QuestPDF**:

```csharp
public sealed class GeradorDePdfDeEncaixe
{
    public byte[] Gerar(double larguraTecidoCm, double consumoCm,
        IReadOnlyList<PosicaoNoRolo> posicoes, IReadOnlyDictionary<string, byte[]> artesPorChave);
}
```

- Página única, escala 1:1, sem margem/cabeçalho/rodapé.
- Cache de imagem por `chave` dentro do documento (QuestPDF já deduplica
  imagens idênticas internamente em alguns cenários, mas replicar o cache
  explícito do código atual é mais seguro/previsível).
- **Ponto de atenção**: verificar se QuestPDF expõe `/UserUnit` do PDF
  (necessário para encaixes com mais de ~508cm de comprimento, que é o
  limite de página padrão do formato PDF — §13 da especificação detalha a
  fórmula exata). Se QuestPDF não expuser isso nativamente, duas saídas:
  1. Pós-processar o PDF gerado, injetando a entrada `/UserUnit` diretamente
     no dicionário da página (manipulação de baixo nível do arquivo PDF,
     usando uma lib como `PdfSharpCore`/`iText7` só para esse ajuste
     pontual, ou edição binária direta como o código Node atual já faz).
  2. Trocar QuestPDF por `PDFsharp`/`iText7` só para esta rotina específica,
     se algum deles tiver suporte nativo mais direto — validar em spike antes
     de decidir a biblioteca definitiva de PDF do projeto todo.
- Sem servidor/rede: a geração roda in-process; não há mais o conceito de
  "sessão" com TTL de 10 minutos guardando artes em memória entre um upload e
  o outro (`artesGuardadas` do código atual) — o ViewModel já tem tudo em
  memória local no momento de gerar o PDF, então o método pode receber as
  artes diretamente como parâmetro.

---

## 14. Integração com WhatsApp

> **Plano antigo, superado — ver aviso no topo do documento.** Mantido como
> referência histórica; não é mais o plano do Disparo (agora captação de lead
> → Supabase, em stand-by).

Esta é a peça de maior risco do porte (já sinalizado em §15 da especificação).
Abordagem recomendada: **sidecar Node.js isolado**.

```
Optimize.App (processo principal .NET)
   │  IPC (named pipe local, ou HTTP restrito a 127.0.0.1)
   ▼
Optimize.WhatsAppSidecar (processo filho, Node.js + whatsapp-web.js)
   │  Puppeteer/Chromium headless
   ▼
WhatsApp Web
```

- O sidecar é um pequeno servidor Node (Express minimalista, ou até só um
  processo com `stdin`/`stdout` como protocolo) empacotado junto no
  instalador (via `pkg`/`nexe`, exatamente como o backend atual já é
  empacotado hoje — reaproveita conhecimento existente do time).
- `Optimize.App` spawna esse processo filho na inicialização (ou sob demanda,
  ao entrar na aba Disparo/Configurações), descobre uma porta livre
  (`TcpListener` em `127.0.0.1:0`, mesmo truque já usado pelo Tauri atual —
  §16 da especificação) e conversa com ele via um contrato simples:

```csharp
public interface IWhatsAppSidecarClient
{
    Task ConectarAsync();
    Task DesconectarAsync();
    Task<ResultadoEnvio> EnviarAsync(string numero, string mensagem, byte[]? midia, string? mimetype);
    IObservable<StatusWhatsApp> Status { get; }     // qr | conectando | pronto | desconectado
    IObservable<string> Logs { get; }
}
```

- Comunicação via HTTP loopback + long-polling simples, ou WebSocket loopback
  (não precisa ser SignalR completo — é comunicação entre dois processos na
  mesma máquina, pode ser bem mais simples que o Socket.IO original, que
  precisava suportar um browser remoto).
- **Normalização de número, template `{{nome}}`, delay aleatório e log CSV
  incremental** (§5.3 da especificação) ficam do lado **.NET**
  (`DisparoService`), não do sidecar — o sidecar só sabe "mandar mensagem X
  para número Y" e reportar status de conexão/QR code. Isso mantém a lógica
  de negócio testável em C# e o sidecar o mais burro/pequeno possível.

**Alternativas a avaliar antes de fechar essa decisão** (registradas para
decisão consciente, não como recomendação definitiva):

1. Bibliotecas que reimplementam o protocolo do WhatsApp Web sem precisar de
   navegador (ex. portes/forks de Baileys) — eliminaria a dependência de
   Chromium embutido, reduzindo bastante o tamanho do instalador. Exige
   validar maturidade e risco de manutenção antes de trocar.
2. WhatsApp Business API oficial — muda o modelo de uso (conta aprovada,
   custo por mensagem, sem QR code), provavelmente fora do espírito atual do
   produto (disparo pessoal/pequena empresa via QR code escaneado uma vez).

---

## 15. Armazenamento local e configuração

Substitui `paths.js`/`config.js` (§7 da especificação):

```csharp
public sealed class CaminhosDoApp
{
    public string PastaDeDados { get; }      // %LOCALAPPDATA%\Optimize (ou %APPDATA%, a decidir)
    public string BancoDeDados => Path.Combine(PastaDeDados, "dados.db");
    public string PastaUploads => Path.Combine(PastaDeDados, "uploads");
    public string ArquivoResultadoDisparo => Path.Combine(PastaDeDados, "resultado-envio.csv");
    public string PastaSessaoWhatsApp => Path.Combine(PastaDeDados, "whatsapp-sessao");
}
```

Resolução: `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)`
+ `"Optimize"` — equivalente direto ao `%APPDATA%\com.arteof.optimize` que o
Tauri atual já usa (mesma ideia, ajustando para o padrão .NET). Sem variável
de ambiente de override por padrão (o `OPTIMIZE_DATA_DIR` do sistema atual
existia para separar dev de produção no empacotamento `pkg`; em .NET,
`dotnet run` local pode simplesmente usar uma pasta de dados de
desenvolvimento diferente via configuração de ambiente do próprio
`IHostEnvironment`).

Configurações do usuário (código do país/DDD padrão, intervalos de disparo,
DPI padrão de exportação) — persistir como uma tabela simples
(`ConfiguracaoApp` chave/valor) no mesmo SQLite, em vez de um arquivo JSON à
parte; evita ter duas fontes de estado para sincronizar.

---

## 16. Injeção de dependência e inicialização

`App.axaml.cs` usa `Microsoft.Extensions.Hosting` (Generic Host), mesmo em app
desktop — permite reaproveitar toda a stack de DI/Logging/Options já madura do
.NET, sem depender de um container próprio do Avalonia:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    var host = Host.CreateDefaultBuilder()
        .ConfigureServices((ctx, services) =>
        {
            services.AddDbContext<OptimizeDbContext>(o => o.UseSqlite($"Data Source={caminhos.BancoDeDados}"));
            services.AddSingleton<CaminhosDoApp>();
            services.AddScoped<IMoldeService, MoldeService>();
            services.AddScoped<IProjetoService, ProjetoService>();
            services.AddScoped<IEncaixeService, EncaixeService>();
            services.AddScoped<IVetorService, VetorService>();
            services.AddScoped<IDisparoService, DisparoService>();
            services.AddSingleton<IWhatsAppSidecarClient, WhatsAppSidecarClient>();
            services.AddTransient<IEnumerable<ILeitorDeMolde>>(/* DXF, PLT, SVG, PDF */);
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<MoldesViewModel>();
            // ... demais ViewModels
        })
        .Build();

    var mainWindow = new MainWindow { DataContext = host.Services.GetRequiredService<MainWindowViewModel>() };
    // aplicar migrations pendentes do EF Core aqui, antes de mostrar a janela
    desktop.MainWindow = mainWindow;
    base.OnFrameworkInitializationCompleted();
}
```

Migrations do EF Core aplicadas automaticamente na inicialização
(`dbContext.Database.Migrate()`), substituindo o `CREATE TABLE IF NOT EXISTS`
+ `garantirColuna` manual do `db.js` atual por um mecanismo de migração
versionado e testável — ganho real sobre o sistema original.

---

## 17. Empacotamento e instalador

```
dotnet publish src/Optimize.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

- **Velopack** empacota o publish acima num instalador `.exe` (ou pacote
  `.nupkg` + bootstrapper), com suporte pronto a auto-update — algo que o
  sistema atual **não tem** (o `.exe` gerado por `pkg`/Tauri é instalado uma
  vez, sem mecanismo de atualização automática documentado). Isso é uma
  melhoria real de produto a considerar incluir desde o início.
- O sidecar Node (`Optimize.WhatsAppSidecar`, §14) é empacotado como recurso
  adicional dentro do instalador (mesma ideia de `bundle.resources` do Tauri
  atual, §16 da especificação) — o instalador final contém tanto o `.exe`
  .NET quanto o `.exe` do sidecar (gerado via `pkg`/`nexe` do lado Node,
  reaproveitando o pipeline de build que já existe hoje para isso).
- Alternativa mais simples (se Velopack se mostrar complexo demais para o
  escopo do time): **Inno Setup** sobre o resultado de `dotnet publish`
  self-contained — instalador tradicional, sem auto-update embutido, mas
  bem mais simples de manter e depurar. Ficaria mais parecido com o build
  atual via `pkg` + NSIS (Tauri), só trocando NSIS por Inno Setup.
- Pasta de dados do usuário (§15) sobrevive a desinstalação/atualização por
  padrão (não fica dentro da pasta de instalação do programa) — mesma
  garantia que o Tauri atual já documenta (`COMO-GERAR-O-EXE.md`: "dados
  preservados entre atualizações do instalador").

---

## 18. Testes e validação contra o sistema atual

- **`Optimize.Core.Tests`**: cobertura prioritária de `Geometria` (área,
  bounding box, Douglas-Peucker), do motor de Encaixe (usando os casos de
  teste e números já documentados no `README.md`/especificação — ver §12
  acima) e do Vetor (comparar métricas de "erro"/"iguais" já documentadas
  para os mesmos desenhos de teste, se os arquivos originais de teste
  puderem ser recuperados/recriados).
- **Testes de parser de molde**: para cada formato (DXF/PLT/SVG/PDF), manter
  um pequeno conjunto de arquivos de exemplo (peça simples, peça com furo,
  arquivo com "a folha"/moldura, arquivo com bloco `INSERT`/`<use>`) e
  comparar contorno+furos+medida resultante contra o que o sistema JS atual
  produz para os mesmos arquivos — a melhor forma de detectar divergência
  sutil de unidade/orientação de eixo.
- **Testes de serviço** (`Optimize.Services.Tests`): usar SQLite in-memory
  (`Microsoft.Data.Sqlite` com `DataSource=:memory:` e conexão mantida aberta)
  para testar CRUD e as regras de faxina de arquivo/cascade sem tocar disco
  real.
- **Testes de ViewModel**: focar nos fluxos com mais lógica reativa (cálculo
  de "peças por unidade × unidades" em Projetos, atualização de resumo de
  DPI em Moldes, agregação de progresso paralelo em Encaixe) — não é
  necessário testar toda a superfície de binding, só a lógica de negócio que
  vive no ViewModel.

---

## 19. Fases de implementação

Adaptação do plano de porte já sugerido em §18 da especificação, agora
organizado por entregas visíveis dentro da arquitetura MVVM:

**Fase 0 — Fundação**
`Optimize.Core.Geometria` + `Optimize.Data` (schema completo + migrations) +
esqueleto do app Avalonia com DI/Hosting funcionando e navegação entre telas
vazias.

**Fase 1 — Moldes (sem arte)**
Leitores DXF/PLT (mais simples que SVG/PDF), CRUD completo de molde, wizard
funcional, listagem com miniatura de contorno.

**Fase 2 — Encaixe (motor + tela, sem NFP/paralelismo ainda)**
`EncaixadorPorContorno` single-thread, tela de Encaixe funcional ponta a
ponta (carregar peças → configurar tecido → encaixar → ver resultado →
exportar PNG), validado numericamente contra os casos de teste documentados.

**Fase 3 — Encaixe completo**
Sistema de receitas/poda/parede, agrupamento em blocos, paralelização,
memória de aprendizado, NFP, geração de PDF com `/UserUnit`.

**Fase 4 — Arte no molde + Projetos**
`Optimize.Core.Arte`, estampas/rapport, tela de Projetos completa
(cliente→projeto→peça), remoção de fundo em lote, integração "mandar para o
encaixe" das duas origens (molde e projeto).

**Fase 5 — Vetor**
Pipeline completo de vetorização, tela com prévias/zoom sincronizado,
atalhos pré-configurados.

**Fase 6 — SVG e PDF como formatos de molde**
Os dois leitores mais arriscados/trabalhosos (§4.2) — deixados por último
propositalmente, pois Fases 1-5 já validam toda a arquitetura com DXF/PLT.

**Fase 7 — Disparo WhatsApp**
Sidecar Node isolado, `DisparoService`, tela de Disparo/Configurações.
Explicitamente a última frente de produção a fechar, dado o risco técnico
já sinalizado em §14.

**Fase 8 — Empacotamento e distribuição**
Instalador (Velopack ou Inno Setup), auto-update (se adotado), assinatura de
código (recomendado para reduzir alertas do SmartScreen do Windows — não
existia menção a isso no sistema atual, vale avaliar incluir).
