# Especificação Técnica do Optimize — para reescrita em .NET

> Documento gerado a partir da leitura integral do código-fonte (≈14.600 linhas
> em JS/Rust/Node) mais a documentação existente (`README.md`, `docs/ESTRUTURA.md`,
> `docs/MAPA.md`). Objetivo: servir de referência única para reimplementar o
> sistema em .NET sem precisar reler o código-fonte original.

> ## ⚠️ Atualização (01/09/2026) — leia antes de usar §5, §12 e §15
>
> O projeto original evoluiu desde que este documento foi escrito. Confirmado
> por leitura direta do código-fonte atual (`C:\projetos\optmize-full`, fora
> deste repositório — não só pelos snapshots de doc em `docs/atualizacao/`):
>
> 1. **Migração de front própria do projeto original** (HTML/JS puro →
>    React+TypeScript+Vite, servidor Express/SQLite/Tauri inalterados) — não
>    afeta este port em .NET/Avalonia diretamente, mas confirma que o domínio
>    puro (`geometria`, `encaixe-motor`, `vetor`, `nfp`, etc.) é a fonte da
>    verdade, não a camada de tela.
> 2. **Disparo/WhatsApp foi removido do projeto original** — confirmado:
>    `server.js`/`package.json` atuais não têm mais `whatsapp-web.js`,
>    Socket.IO, nem nada de disparo. Decisão tomada com o usuário (01/09/2026):
>    o módulo de Disparo do port **não é mais disparo de mensagens em massa via
>    WhatsApp** — vira **captação de lead**, recebendo dados e enviando pra uma
>    base separada (Supabase) via notificação/webhook silencioso (sem alertar
>    o usuário do Optimize). Detalhes de onde o lead se origina ainda não
>    foram definidos — módulo em **stand-by**. §5 e §15 abaixo ficam como
>    registro histórico de como o sistema ANTIGO (WhatsApp em massa)
>    funcionava, não como plano do port.
> 3. **Componente investigado e documentado nesta atualização, e já portado
>    (01/09/2026): `encaixe-rede.js`** — uma rede neural pequena (feed-forward,
>    sem biblioteca) que complementa (não substitui) a memória por assinatura
>    exata descrita em §12, generalizando pra trabalho parecido. Algoritmo
>    completo, pesos, treino e integração na busca documentados em §12.1, com
>    uma nota ao final marcando exatamente onde cada peça vive no port .NET —
>    `OptimizePro.Core.Encaixe.Busca.RedeDeReceitas`/`VocabularioDeReceita`/
>    `VetorizacaoDoTrabalho`/`AssinaturaDeTrabalho` (Core, matemática pura),
>    `OptimizePro.Services.Encaixe.EncaixeMemoriaService` (treino/retreino/
>    consulta) e `EncaixeService` (pesagem e poda da busca).

## Índice

1. [Visão geral](#1-visão-geral)
2. [Arquitetura atual e proposta de arquitetura .NET](#2-arquitetura-atual-e-proposta-de-arquitetura-net)
3. [Modelo de dados (schema SQLite)](#3-modelo-de-dados-schema-sqlite)
4. [API REST](#4-api-rest)
5. [Comunicação em tempo real (Socket.IO → SignalR)](#5-comunicação-em-tempo-real-socketio--signalr)
6. [Upload de arquivos e faxina de disco](#6-upload-de-arquivos-e-faxina-de-disco)
7. [Configuração e caminhos de dados](#7-configuração-e-caminhos-de-dados)
8. [Módulo Moldes](#8-módulo-moldes)
9. [Arte dentro do molde e Estampas](#9-arte-dentro-do-molde-e-estampas)
10. [Módulo Projetos](#10-módulo-projetos)
11. [Motor de Encaixe (nesting) — núcleo do sistema](#11-motor-de-encaixe-nesting--núcleo-do-sistema)
12. [Memória de aprendizado do Encaixe](#12-memória-de-aprendizado-do-encaixe)
13. [Geração de PDF do encaixe](#13-geração-de-pdf-do-encaixe)
14. [Módulo Vetor (raster → SVG)](#14-módulo-vetor-raster--svg)
15. [Disparo de mensagens WhatsApp](#15-disparo-de-mensagens-whatsapp)
16. [Empacotamento desktop (Tauri → proposta .NET)](#16-empacotamento-desktop-tauri--proposta-net)
17. [Regras transversais e armadilhas já pagas](#17-regras-transversais-e-armadilhas-já-pagas)
18. [Plano de porte sugerido](#18-plano-de-porte-sugerido)

---

## 1. Visão geral

O **Optimize** é um sistema desktop local (empacotado com Tauri sobre um backend
Node.js) com duas frentes:

- **Produção**: biblioteca de moldes de costura (geometria vetorial), projetos de
  cliente (arte já aplicada), encaixe de peças em tecido (nesting) e vetorização
  de imagem (raster → SVG).
- **Comunicação**: disparo de mensagens no WhatsApp via `whatsapp-web.js`.

Stack atual: **Node.js** (Express + Socket.IO + better-sqlite3 + pdfkit +
whatsapp-web.js) no backend; **HTML/CSS/JS puro** (sem framework) no frontend;
**Rust/WebAssembly** para o laço quente do encaixe; **Tauri** (Rust) como shell
desktop que sobe o backend Node como processo filho e exibe a UI num WebView2
apontando para `http://127.0.0.1:{porta}`.

Não há login/autenticação — é um app local de usuário único.

As duas telas de biblioteca **não são a mesma coisa** e essa distinção deve ser
preservada no modelo de domínio .NET:

- **Moldes** guarda a **geometria** da peça (contorno em cm). A estampa é
  aplicada depois, em qualquer tamanho (P/M/G).
- **Projetos** guarda a **arte já aplicada e finalizada** (a estampa na
  camisa/bandeira pronta) — vai direto para o encaixe, sem mais nenhum passo.

---

## 2. Arquitetura atual e proposta de arquitetura .NET

### 2.1 Atual

```
Tauri (Rust, shell desktop)
  └─ spawna processo filho: Optimize-server.exe (backend Node empacotado via pkg)
       └─ Express + Socket.IO servindo em http://127.0.0.1:{porta livre}
            ├─ public/*.html/css/js  (frontend estático, servido pelo próprio Express)
            ├─ SQLite (better-sqlite3)  → dados.db
            ├─ uploads/ (artes de molde e de projeto, em disco)
            └─ whatsapp-web.js (Puppeteer/Chromium headless)
  └─ WebView2 aponta para essa URL local
```

Dois caminhos de build (sequenciais, não alternativos): `pkg` empacota o
Node/Express num `.exe` standalone; o Tauri empacota esse `.exe` como recurso
interno e gera o instalador NSIS final.

### 2.2 Proposta equivalente em .NET

Como .NET não tem a mesma separação de runtime que existe entre Rust e Node, a
arquitetura pode **colapsar em um único processo**:

```
App desktop .NET (WinUI 3 / WPF / MAUI, com WebView2)
  └─ hospeda um Kestrel/ASP.NET Core embutido (Minimal API), mesmo processo
       ├─ wwwroot/  (frontend — pode ser Blazor, ou o mesmo HTML/JS/CSS reaproveitado)
       ├─ SQLite (Microsoft.Data.Sqlite / EF Core)  → dados.db
       ├─ uploads/ (mesma estrutura de pastas)
       ├─ SignalR (equivalente ao Socket.IO)
       └─ integração WhatsApp (ver seção 15 — sem equivalente direto)
  └─ WebView2 aponta para http://127.0.0.1:{porta} OU renderiza Blazor Hybrid direto
```

Vantagens de colapsar em um processo único: elimina a complexidade de
descobrir porta livre + spawnar processo filho + esperar o servidor subir +
matar o processo filho ao sair — tudo já citado como lógica explícita em
`src-tauri/src/main.rs`.

**Bibliotecas sugeridas:**

| Necessidade atual (Node) | Equivalente .NET |
|---|---|
| Express + rotas REST | ASP.NET Core Minimal API / Controllers |
| better-sqlite3 | Microsoft.Data.Sqlite ou EF Core (Sqlite provider) |
| Socket.IO | SignalR (client precisa ser reescrito; protocolo de fio é diferente) |
| pdfkit | QuestPDF, PDFsharp ou iText7 (verificar suporte a `/UserUnit`) |
| whatsapp-web.js (Puppeteer) | Sem equivalente direto — ver seção 15 |
| Canvas2D (frontend) | SkiaSharp (server-side/WASM) para geração de imagem/prévia |
| Web Workers (paralelismo) | `Task.Run`/`Parallel` — mais simples, memória compartilhada direta |
| WebAssembly (Rust) | Pode-se portar a lógica para C# puro (`Span<int>`/`unsafe`) — não é necessário WASM dentro de um app .NET nativo |
| Tauri (shell) | WinUI 3 / WPF + `Microsoft.Web.WebView2` |
| pkg (empacotar Node em .exe) | Publicação nativa .NET (`dotnet publish -r win-x64 --self-contained`) |

---

## 3. Modelo de dados (schema SQLite)

Motor atual: `better-sqlite3` (síncrono). Arquivo `dados.db`. Pragmas:
`journal_mode=WAL`, `foreign_keys=ON`, `optimize` ao final da inicialização.

Todas as tabelas usam `CREATE TABLE IF NOT EXISTS`; colunas adicionadas depois
da criação inicial são migradas via `garantirColuna(tabela, coluna, definicao)`
(`PRAGMA table_info` + `ALTER TABLE ... ADD COLUMN` condicional). Hoje só há
uma: `projeto_pecas.miniatura`.

### 3.1 Tabela `moldes`

| coluna | tipo | obs |
|---|---|---|
| id | INTEGER PK AUTOINCREMENT | |
| nome | TEXT NOT NULL | |
| observacoes | TEXT | nullable |
| criado_em | TEXT NOT NULL | ISO 8601 |
| atualizado_em | TEXT | nullable |

### 3.2 Tabela `molde_pecas`

| coluna | tipo | obs |
|---|---|---|
| id | INTEGER PK AUTOINCREMENT | |
| molde_id | INTEGER NOT NULL | FK → `moldes(id)` ON DELETE CASCADE |
| tamanho | TEXT NOT NULL DEFAULT 'único' | |
| papel | TEXT NOT NULL | frente, costas, manga direita, manga esquerda, manga, gola, punho, cós, bolso, vista, forro, outro |
| nome | TEXT | nullable |
| quantidade | INTEGER NOT NULL DEFAULT 1 | |
| largura | REAL NOT NULL | cm |
| altura | REAL NOT NULL | cm |
| contorno | TEXT NOT NULL | JSON: `[{x,y}, ...]` (≥3 pontos) |
| furos | TEXT | JSON: `[[{x,y},...], ...]` ou NULL |
| origem | TEXT | nullable, ex: "DXF · mm" |
| ordem | INTEGER NOT NULL DEFAULT 0 | ordena na listagem |

### 3.3 Tabela `molde_artes` (estampas)

| coluna | tipo | obs |
|---|---|---|
| id | INTEGER PK AUTOINCREMENT | |
| molde_id | INTEGER NOT NULL | FK → `moldes(id)` ON DELETE CASCADE |
| nome | TEXT NOT NULL | nome da estampa |
| criado_em | TEXT NOT NULL | |
| atualizado_em | TEXT | nullable |

### 3.4 Tabela `molde_arte_pecas`

| coluna | tipo | obs |
|---|---|---|
| id | INTEGER PK AUTOINCREMENT | |
| arte_id | INTEGER NOT NULL | FK → `molde_artes(id)` ON DELETE CASCADE |
| papel | TEXT NOT NULL | parte da peça que a arte cobre |
| arquivo | TEXT NOT NULL | nome físico em `uploads/artes-molde` |
| nome_original | TEXT | nullable |
| ajuste | TEXT | JSON: `{tipo, modo, escala, x, y, giro, ppcmArquivo}` |

### 3.5 Tabela `encaixe_receitas`

| coluna | tipo | obs |
|---|---|---|
| id | INTEGER PK AUTOINCREMENT | |
| assinatura | TEXT NOT NULL | agrupa trabalhos "parecidos" (§12) |
| receita | TEXT NOT NULL | motor+agrupamento+ordem+heurística |
| usos | INTEGER NOT NULL DEFAULT 0 | |
| vitorias | INTEGER NOT NULL DEFAULT 0 | |
| atualizado_em | TEXT NOT NULL | |
| — | UNIQUE(assinatura, receita) | usado em upsert `ON CONFLICT` |

### 3.6 Tabela `encaixe_guardados`

| coluna | tipo | obs |
|---|---|---|
| chave | TEXT PRIMARY KEY | identifica o trabalho **exato** |
| assinatura | TEXT | nullable |
| largura_tecido | REAL | nullable |
| espaco | REAL | nullable |
| margem | REAL | nullable |
| consumo | REAL NOT NULL | metragem — quanto menor, melhor |
| aproveitamento | REAL | nullable, % |
| pecas | TEXT | JSON (opcional) |
| posicoes | TEXT NOT NULL | JSON: posição de cada peça |
| receita | TEXT | nullable |
| criado_em | TEXT NOT NULL | |
| atualizado_em | TEXT | nullable |

### 3.7 Tabela `projeto_clientes`

> **Nome proposital, não `clientes`**: instalações antigas têm uma tabela
> `clientes` legada (módulo comercial removido) com colunas diferentes. Um
> `CREATE TABLE IF NOT EXISTS clientes` não criaria nada — o código passaria a
> ler a tabela velha com colunas erradas. **Ao portar para .NET/EF Core, não
> usar o nome `Cliente`/`clientes` para esta entidade** se algum dia precisar
> importar bancos antigos.

| coluna | tipo | obs |
|---|---|---|
| id | INTEGER PK AUTOINCREMENT | |
| nome | TEXT NOT NULL | |
| observacoes | TEXT | nullable |
| criado_em | TEXT NOT NULL | |
| atualizado_em | TEXT | nullable |

### 3.8 Tabela `projetos`

| coluna | tipo | obs |
|---|---|---|
| id | INTEGER PK AUTOINCREMENT | |
| cliente_id | INTEGER NOT NULL | FK → `projeto_clientes(id)` ON DELETE CASCADE |
| nome | TEXT NOT NULL | |
| observacoes | TEXT | nullable |
| largura_tecido | REAL | nullable |
| espaco | REAL | nullable (pode ser 0) |
| margem | REAL | nullable (pode ser 0) |
| giro | TEXT | `"180"` \| `"fixa"` \| `"livre"` \| NULL |
| criado_em | TEXT NOT NULL | |
| atualizado_em | TEXT | nullable |

Índice: `idx_projetos_cliente(cliente_id)`.

### 3.9 Tabela `projeto_pecas`

| coluna | tipo | obs |
|---|---|---|
| id | INTEGER PK AUTOINCREMENT | |
| projeto_id | INTEGER NOT NULL | FK → `projetos(id)` ON DELETE CASCADE |
| nome | TEXT NOT NULL | |
| arquivo | TEXT NOT NULL | nome físico em `uploads/projetos` |
| largura | REAL NOT NULL | cm |
| altura | REAL NOT NULL | cm |
| quantidade | INTEGER NOT NULL DEFAULT 1 | |
| ordem | INTEGER NOT NULL DEFAULT 0 | |
| miniatura | TEXT | **coluna migrada**; data URL ~240px, limite 200.000 chars, nullable |

Índice: `idx_projeto_pecas_projeto(projeto_id)`.

### 3.10 Tabela `encaixe_historico`

| coluna | tipo | obs |
|---|---|---|
| id | INTEGER PK AUTOINCREMENT | |
| assinatura | TEXT NOT NULL | |
| largura_tecido | REAL | nullable |
| pecas | INTEGER | nullable — contagem |
| consumo | REAL | nullable |
| aproveitamento | REAL | nullable |
| receita | TEXT | nullable |
| tentativas | INTEGER | nullable |
| criado_em | TEXT NOT NULL | |
| features | TEXT | **novo (ver §12.1)** — JSON do vetor de 12 números do trabalho (`vetorDoTrabalho`), nullable; null em linhas gravadas antes da rede das receitas existir |
| placar | TEXT | **novo (ver §12.1)** — JSON do placar de todas as receitas tentadas nesta busca (`[{receita, tentativas, vitorias}, ...]`), nullable pelo mesmo motivo |

### 3.10.1 Tabela `encaixe_rede_pesos` (nova — ver §12.1)

| coluna | tipo | obs |
|---|---|---|
| id | INTEGER PK | sempre `1` — uma linha só, reescrita a cada retreino |
| pesos | TEXT NOT NULL | JSON dos pesos/vieses da rede (`{tamanhos, camadas: [{pesos, vies}, ...]}`) |
| exemplos | INTEGER NOT NULL DEFAULT 0 | quantos exemplos de treino foram usados no último retreino |
| atualizado_em | TEXT NOT NULL | |

### 3.11 Tabelas legadas (não recriar, não mapear)

Instalações antigas podem conter: `clientes`, `produtos`, `producoes`, `lojas`,
`notas`, `pagamentos`, `orcamentos`, `configuracoes` (módulo comercial/financeiro
removido). Nenhum código atual lê/escreve nelas — preservadas só por precaução.
**Ao portar, ignorar completamente**; não mapear como entidades EF Core.

### 3.12 Serialização JSON em colunas TEXT

Campos `contorno`, `furos`, `ajuste`, `posicoes`, `pecas` (em `encaixe_guardados`)
são strings JSON dentro de colunas TEXT — não viram colunas relacionais.
Em .NET, usar `System.Text.Json` para (de)serializar antes de persistir, ou
mapear como `owned entity`/`value converter` no EF Core.

---

## 4. API REST

Body limits atuais por rota: `/api/encaixe` (pdf) 20MB, `/api/encaixe` (memória)
2MB, resto do app 15MB (global). Uploads de imagem usam `express.raw` com
content-type curinga (300MB moldes/projetos, 400MB artes de encaixe) — em
ASP.NET Core, ler `Request.Body` como stream bruto, sem model binding, com
`RequestSizeLimit`/Kestrel `MaxRequestBodySize` configurados por rota.

### 4.1 Rota solta em `server.js`

**GET `/resultado-envio.csv`** — 404 texto se não existir; senão `res.download()`
do CSV de resultados do disparo.

### 4.2 Rotas `/api/moldes` (moldes-api.js)

| Método | Rota | Entrada | Saída / efeito |
|---|---|---|---|
| GET | `/api/moldes/papeis` | — | `{papeis: string[]}` lista fixa de 12 papéis |
| GET | `/api/moldes/` | — | array de moldes + `tamanhos`, `totalPecas`, `pecasPorUnidade` calculados |
| GET | `/api/moldes/:id` | — | molde + `pecas` (contorno/furos já parseados) + `artes` |
| POST | `/api/moldes/` | `{nome, observacoes, pecas[]}` | valida cada peça via `arrumarPeca`; 400 se nenhuma válida; insere molde+peças; `{id, pecas: qtd}` |
| PUT | `/api/moldes/:id` | idem POST | 404 se não existe; **substitui peças por inteiro** (delete+reinsert); `{ok, pecas}` |
| DELETE | `/api/moldes/:id` | — | cascade apaga peças/artes; limpa arquivos órfãos de arte; `{ok}` |
| POST | `/api/moldes/:id/artes/imagem?papel=` | bytes crus | detecta ext. por assinatura binária (fallback por Content-Type); grava em `uploads/artes-molde`; `{ok, arquivo, url, bytes}` — **não** grava no banco |
| GET | `/api/moldes/:id/artes` | — | array de estampas com `pecas:[{papel,arquivo,url,nomeOriginal,ajuste}]` |
| POST | `/api/moldes/:id/artes` | `{id?, nome, pecas[]}` | upsert de estampa (por `id` opcional); substitui peças por inteiro; limpa arquivos órfãos; `{id, pecas: qtd}` |
| DELETE | `/api/moldes/:id/artes/:arteId` | — | cascade + limpa arquivos; `{ok}` |

**Ajuste de arte** (`arrumarAjuste`, validação server-side):
```
modo: "cobrir"|"caber"|"esticar" (default "cobrir")
escala: clamp [10,400] (default 100)
x, y: número (default 0)
giro: arredondado ao múltiplo de 90 mais próximo, normalizado [0,360)
```

### 4.3 Rotas `/api/projetos` (projetos-api.js)

| Método | Rota | Entrada | Saída / efeito |
|---|---|---|---|
| GET | `/api/projetos/clientes` | — | clientes + contagem de projetos (LEFT JOIN) |
| POST | `/api/projetos/clientes` | `{nome, observacoes?}` | nome máx 120, obs máx 500; `{id, nome}` |
| PUT | `/api/projetos/clientes/:id` | idem | 404/400; `{ok}` |
| DELETE | `/api/projetos/clientes/:id` | — | cascade projetos+peças; limpa arquivos; `{ok}` |
| GET | `/api/projetos/clientes/:id/projetos` | — | `{cliente, projetos[]}` cada um com `pecas`, `pecasPorUnidade`, `capa` (miniatura da 1ª peça) |
| GET | `/api/projetos/:id` | — | `{...projeto, cliente, pecas[]}` com `url` |
| POST | `/api/projetos/` | `{clienteId, nome}` | `{id, nome}` |
| PUT | `/api/projetos/:id` | `{nome, observacoes?, larguraTecido?, espaco?, margem?, giro?, pecas[]}` | valida cada peça (arquivo, largura/altura>0, quantidade≥1, miniatura data-URL<200000); substitui peças por inteiro; `{ok, pecas}` |
| DELETE | `/api/projetos/:id` | — | cascade + limpa arquivos; `{ok}` |
| PATCH | `/api/projetos/:id/miniaturas` | `{miniaturas:[{id,miniatura}]}` | grava **só** a coluna miniatura, sem tocar nos outros campos; `{ok, guardadas}` |
| POST | `/api/projetos/:id/imagem` | bytes crus | detecta ext. por assinatura binária (sem fallback); grava em `uploads/projetos`; `{ok, arquivo, url, bytes}` |

### 4.4 Rotas `/api/encaixe` (encaixe-memoria.js)

| Método | Rota | Entrada | Saída / efeito |
|---|---|---|---|
| GET | `/api/encaixe/memoria?assinatura=` | — | `{memoria:{receita:{usos,vitorias}}, encaixesDoTipo, encaixesNoTotal, melhorAntes}` — combina camada geral (peso 0.4/1) e camada do tipo (peso 2x) |
| POST | `/api/encaixe/memoria` | `{assinatura, receita, placar?, larguraTecido?, pecas?, consumo?, aproveitamento?, tentativas?}` | upsert incremental de usos/vitórias por receita; sempre insere linha em `encaixe_historico`; `{ok, encaixesDoTipo}` |
| GET | `/api/encaixe/guardado?chave=` | — | `{guardado: {...}|null}` |
| POST | `/api/encaixe/guardado` | `{chave, consumo, posicoes, ...}` | **só substitui se `consumo` novo < consumo salvo** (empate não troca); `{guardado:bool, melhorGuardado}` |
| DELETE | `/api/encaixe/memoria` | — | apaga TUDO de `encaixe_receitas`, `encaixe_historico`, `encaixe_guardados`; `{ok}` |

### 4.5 Rotas `/api/encaixe` (encaixe-pdf.js)

| Método | Rota | Entrada | Saída / efeito |
|---|---|---|---|
| POST | `/api/encaixe/arte?sessao=&chave=` | bytes crus | guarda em `Map` **em memória** (não disco/banco), TTL 10min; `{ok, bytes}` |
| POST | `/api/encaixe/pdf` | `{larguraTecido, consumo, imagens[], posicoes[], nome?, sessao?}` | monta PDF em página única, escala 1:1, com `/UserUnit` se página > 508cm; stream direto, sem salvar em disco (§13) |

---

## 5. Comunicação em tempo real (Socket.IO → SignalR)

> **Registro histórico, não plano de port** — ver aviso no topo do documento.
> Esta seção descreve o disparo de WhatsApp em massa do sistema atual; a
> direção decidida pro nosso port é diferente (captação de lead → Supabase) e
> está em stand-by. Mantido aqui só como referência de como o sistema original
> funciona, caso o desenho futuro do Disparo precise reaproveitar peças disso
> (ex.: normalização de número, no formato §5.3).

`new Server(server, {maxHttpBufferSize: 1.5e7})` (~15MB, para mídia em base64).

### 5.1 Ao conectar

Servidor emite ao socket recém-conectado: **`status`** → string do `waStatus`
atual (`"desconectado"|"iniciando"|"qr"|"autenticando"|"pronto"`).

### 5.2 Eventos recebidos do cliente

| evento | payload | efeito |
|---|---|---|
| `connect-whatsapp` | — | inicializa `whatsapp-web.js` `Client` com `LocalAuth`; Puppeteer headless procurando Edge/Chrome instalado; `waStatus="iniciando"` |
| `logout-whatsapp` | — | logout + destroy do client; remove pasta de sessão; `waStatus="desconectado"` |
| `stop-dispatch` | — | seta flag `stopRequested=true` |
| `start-dispatch` | ver 5.3 | inicia disparo sequencial |

### 5.3 Payload de `start-dispatch`

```ts
{
  contacts: [{numero, nome}],
  message: string,
  minDelay?: number,   // ms
  maxDelay?: number,   // ms
  media?: { base64, mimetype, filename? },
  defaultCountryCode?: string,
  defaultDdd?: string,
  lookupNames?: boolean  // default true
}
```

**Normalização de número** (`normalizeNumber`):
```
remove não-dígitos; se >11 dígitos e começa com "0", remove zeros à esquerda
cc = código país (default "55"); ddd = ddd padrão
≤9 dígitos → prefixa DDD, depois CC
10-11 dígitos → prefixa só CC
12+ dígitos → mantém como está
```

**Loop de disparo** (sequencial, `for` com `await`):
1. Se `stopRequested`, encerra.
2. Normaliza número; `waClient.getNumberId(numero)` — se null, erro "não registrado".
3. Se sem nome e `lookupNames`, busca `getContactById` (pushname/name/shortName).
4. Substitui `{{nome}}`/`{{ nome }}` (regex) na mensagem.
5. Envia (`sendMessage`, com ou sem mídia).
6. Grava linha em `resultado-envio.csv` (csv-stringify, header só na 1ª vez).
7. Emite `dispatch-progress`.
8. Delay aleatório uniforme entre `minDelay` e `maxDelay` antes do próximo (exceto no último).

### 5.4 Eventos emitidos pelo servidor (broadcast)

| evento | payload |
|---|---|
| `status` | string |
| `qr` | data URL PNG do QR code |
| `log` | string |
| `dispatch-started` | `{total}` |
| `dispatch-progress` | `{index, total, numero, nome, status, detalhe}` |
| `dispatch-done` | `{enviados, falhas}` |

### 5.5 Eventos internos do whatsapp-web.js → status

`"qr"` → status=`qr`, gera data URL, emite `qr`+`log`. `"authenticated"` →
status=`autenticando`. `"auth_failure"` → status=`desconectado`, client=null.
`"ready"` → status=`pronto`. `"disconnected"` → status=`desconectado`,
client=null.

### 5.6 Lado cliente (public/app.js)

**Parsing de contatos**: texto colado, linha a linha, `numero,nome` (nome
opcional, tudo após a 1ª vírgula). CSV: mesma lógica, com detecção de cabeçalho
(`/^numero/i`) e concatenação ao texto já digitado — **não** há parser CSV real
(sem tratamento de aspas/escapes).

Nenhuma normalização de telefone ocorre no cliente — só no servidor. O cliente
só envia `defaultCountryCode`/`defaultDdd` como configuração.

Nenhuma substituição de `{{nome}}` ocorre no cliente — mensagem enviada literal.

Lista estática de 67 DDDs brasileiros (`DDDS`) para popular o `<select>`.

Delay: `minDelay = max(1, input||8)*1000`; `maxDelay = max(minDelay/1000,
input||20)*1000` (garante max≥min).

**Nota importante**: não foi encontrado no cliente o handler de download do CSV
de resultado — só o botão é mostrado/escondido. Se necessário reproduzir esse
fluxo, confirmar onde o download realmente ocorre (provavelmente via link para
`GET /resultado-envio.csv`, ver §4.1).

---

## 6. Upload de arquivos e faxina de disco

Compartilhado por Moldes (`uploads/artes-molde`) e Projetos (`uploads/projetos`).

**Detecção de tipo por assinatura binária** (nunca pela extensão declarada):
```
PNG:  bytes[0..3] == 0x89 'P' 'N' 'G'
JPG:  bytes[0..1] == 0xFF 0xD8
WEBP: ascii[0..4]=="RIFF" && ascii[8..12]=="WEBP"
GIF:  ascii[0..3]=="GIF"
```

**Nome sem colisão**: `` `${prefixo}-${Date.now()}-${randomBase36(6)}.${ext}` ``.

**Faxina do disco** (`limparImagensSoltas`) — regra que não pode ser
afrouxada: a conferência do que ainda está em uso é contra a **tabela inteira**
(todos os registros vivos), nunca contra a lista que acabou de mudar — a mesma
imagem pode estar referenciada por outro registro. Algoritmo: monta `Set` de
todos os arquivos em uso via query preparada; para cada candidato a apagar não
presente no set, `unlink` (erros ignorados).

---

## 7. Configuração e caminhos de dados

### 7.1 `paths.js`

```
appDir = process.pkg ? dirname(execPath) : __dirname
dataDir = env.OPTIMIZE_DATA_DIR || join(appDir, "Optimize-dados")
```

Em modo empacotado (pkg), dados mutáveis ficam **fora** do binário read-only,
numa pasta gravável ao lado do executável. Pode ser sobrescrito por variável de
ambiente.

| propriedade | caminho | conteúdo |
|---|---|---|
| `dbPath` | `{dataDir}/dados.db` | SQLite |
| `uploadsDir` | `{dataDir}/uploads` | imagens |
| `resultsFile` | `{dataDir}/resultado-envio.csv` | log de disparos |
| `sessionDir` | `{dataDir}/.wwebjs_auth` | sessão WhatsApp (LocalAuth) |
| `cacheDir` | `{dataDir}/.wwebjs_cache` | cache whatsapp-web.js |

Em .NET, equivalente natural: `Environment.SpecialFolder.LocalApplicationData`
ou `%APPDATA%` (como já faz o Tauri: `app.path().app_data_dir()`, resolvendo
para `%APPDATA%\com.arteof.optimize`).

### 7.2 `config.js` — defaults do disparo

```js
{
  message: "Olá {{nome}}, tudo bem? Essa é uma mensagem automática de teste do disparo.",
  minDelayMs: 8000,
  maxDelayMs: 20000,
  defaultCountryCode: "55",
  defaultDdd: "11",
}
```

Porta: `process.env.PORT || 8000`.

---

## 8. Módulo Moldes

### 8.1 Parsers de formato vetorial (`public/moldes.js`)

**Pipeline comum a todos os formatos** — cada leitor produz:
```
linhas: [{ pontos:[{x,y}], fechada:bool }, ...]   // traços soltos, unidade do arquivo
textos: [{ texto, x, y }, ...]                      // rótulos, para nomear peças
```
Depois processado por `montarMoldes(linhas, textos, unidade, avisos, formato, inverterY, modo)`:
1. **`montarLacos`** — costura traços soltos em contornos fechados por proximidade.
2. **`separarPecasEFuros`** — classifica laços como peça/furo, descarta "a folha", remove duplicatas.
3. Conversão para cm (com inversão de Y para DXF/PLT/PDF, que crescem para cima).
4. Nomeação pelo texto cujo ponto cai dentro do contorno (ray-casting).
5. Filtro final: descarta peças com lado ≤0.2cm.

Existe também o **modo "arte" (arquivo inteiro)**: quando o arquivo é uma arte
solta, não um marcador — rasteriza tudo numa grade (420 células no lado maior),
agrupa manchas conectadas, extrai contorno externo por marching-squares
simplificado, funde múltiplas manchas em casco convexo se necessário.

**Costura de traços (`montarLacos`):**
```
tolerancia = max(maiorLadoDaCaixaGeral * 5e-4, 1e-6)
para cada traço solto:
  busca outro traço cuja ponta bate no fim do corrente (tolerância), concatena
  (invertendo se necessário), repete até fechar ou não achar mais candidato
```

**Separação peça/furo (`separarPecasEFuros`):**
- Área com sinal (shoelace) + bounding box de cada laço, ordenados por área.
- `tirarRepetidos`: remove laços com bbox+área coincidentes (peça desenhada 2x).
- `ehAFolha`: laço cobre ≥90% da bbox geral **e** contém outro laço ≥15% de sua
  própria área → é a moldura de página, descarta.
- Para cada laço restante (maior→menor): procura "pai" (laço maior cuja bbox
  contém o ponto e o teste ponto-em-polígono confirma) → vira furo dele; senão
  vira peça nova (se área ≥ mínimo).

#### DXF

Parser de pares código/valor (`0`=tipo de entidade, `10/20`=x/y, etc.), não
usa lib externa. Só ASCII (binário é recusado). Seções `HEADER`/`ENTITIES`/`BLOCKS`.

Entidades suportadas: `LINE`, `LWPOLYLINE` (com bulge → arco via fórmula de
tangente), `POLYLINE`+`VERTEX`, `ARC`, `CIRCLE`, `ELLIPSE` (paramétrica com
rotação), `SPLINE` (avaliação De Boor de B-spline), `INSERT` (resolve bloco
recursivo até profundidade 8, com translação+escala+rotação), `TEXT`/`MTEXT`/`ATTRIB`
(viram `textos`).

**Bulge → arco:**
```
ângulo = 4*atan(bulge)
corda = |p2-p1|; raio = corda/(2*sin(ângulo/2))
altura = raio*cos(ângulo/2)
centro = ponto_médio(p1,p2) + perpendicular_esquerda(p1→p2)*altura
```

**Unidade**: `$INSUNITS` do cabeçalho (1=pol, 2=pé, 4=mm, 5=cm, 6=m) → fator
para cm. Prioridade: forçada pelo usuário > `$INSUNITS` > chute por tamanho da
bbox (>300→mm, >12→cm, senão polegada).

#### PLT / HP-GL

Tokenizador próprio (2 letras = comando; `LB` consome até terminador ETX ou
`DT`-custom; `PE` consome até `;`).

Comandos: `PU`/`PD` (caneta cima/baixo), `PA`/`PR` (absoluto/relativo), `AA`/`AR`
(arco por centro), `CI` (círculo), `PE` (polilinha comprimida — decodificação
base-64/32 bit-a-bit com prefixos `=`/`<`), `LB` (texto), `SC`/`IP` (escala
própria, não aplicada — só gera aviso).

**Decodificação PE**: modo 6-bit padrão (códigos `63-126` continuam, `191-254`
terminam); troca para modo 5-bit ao encontrar `"7"`. Bit menos significativo do
valor decodificado = sinal.

**Unidade**: sem cabeçalho. Padrão `plu` (1/1016 polegada). Chute testa
`[plu,mil,mm,cm]` em ordem e usa a primeira cujo maior lado cai em `5cm..800cm`.

#### SVG

**Delega ao DOM do navegador** (`DOMParser` + `getPointAtLength`/`getCTM`) —
não há parser manual de path/Bézier. Isso significa que, em .NET, será
necessário implementar path-flattening manualmente (ou usar SkiaSharp) já que
não existe equivalente direto fora do browser.

Resolve `<use>` (substitui por clone + transform), ignora conteúdo de
`<defs>/<clipPath>/<mask>/<symbol>` (moldes de repetição). Detecta subpaths
(furos) por salto de amostragem >4× o passo esperado.

**Unidade**: `width`/`height` do SVG raiz; se sem unidade explícita, assume 96
px/polegada CSS e avisa.

#### PDF (vetorial)

Parser artesanal, **sem usar a tabela xref** (varre o arquivo inteiro por regex
`\d+ \d+ obj`, propositalmente robusto a xref corrompido). Suporta streams
comprimidos (Flate) e Object Streams (ObjStm) do PDF moderno; desfaz preditor
PNG quando declarado.

Interpretador de content stream: `q/Q/cm` (pilha de matriz), `m/l/c/v/y/h/re`
(path), `n/f/F/f*/S/s/B/B*/b/b*` (pintar), `Do` (XObject de formulário,
recursivo até profundidade 6), operadores de texto (só para extrair posição/
conteúdo, não geometria de glifo).

Recusa PDF protegido por senha (`/Encrypt`) e não lê PDF escaneado (sem desenho
vetorial). Unidade: pontos (1pt=1/72"); `PDF_PT_POR_CM = 72/2.54`.

### 8.2 Funções de geometria (`public/geometria.js`)

Arquivo puro (sem `document`/`window`), carregado também em Web Workers.
`GEO_EPSILON = 1e-9`.

```
areaComSinal(pontos): shoelace com sinal — Σ(Pi.x*Pi+1.y − Pi+1.x*Pi.y)/2
caixaDeContorno(pontos): bounding box {minX,minY,maxX,maxY,largura,altura}
ladoMenorDoContorno(pontos): min(largura,altura) da bbox
distanciaEntre(a,b): Math.hypot
distanciaAteSegmento(p,a,b): projeção clampada em [0,1] sobre o segmento
simplificar(pontos, tolerancia): Douglas-Peucker ITERATIVO (pilha, não recursão)
```

Recomendação: portar como classe estática `Geometria` em C#, sem dependências
de UI — é o ponto de partida mais seguro do porte.

### 8.3 Fluxo de criação de molde (`public/moldes-tela.js`)

**Wizard de 3 passos:**

1. **Tipo**: `camisa|regata|short|banner|outro` — cada um sugere um conjunto
   padrão de partes (ex: camisa → frente, costas, manga direita, manga
   esquerda, gola). "Outro" pede nome livre.
2. **Quantidade de pedaços e tamanhos**: nº de partes (clamp 1-60); tamanhos em
   texto livre (`"P, M, G GG"`, parseado por split em `[,;/]+|\s+`). Tamanho
   novo herda estrutura de partes do primeiro tamanho já existente.
3. **Uma vaga por parte, por tamanho** (abas por tamanho):

```ts
Parte (em memória) {
  id, papel, papelEscrito, quantidade,
  tamanho, nome, largura, altura,  // cm, 1 casa decimal
  contorno, furos, origem
}
```

Papéis: `frente, costas, manga direita, manga esquerda, manga, gola, punho,
cós, bolso, vista, forro, outro`.

**Upload por vaga**: se o arquivo trouxer 1 peça, preenche a vaga clicada; se
trouxer várias (marcador inteiro), a 1ª preenche a vaga e as demais vão para
vagas vazias seguintes (criando novas se faltar espaço).

**Adivinhação de papel pelo nome** (regex): detecta manga direita/esquerda
(dir/d vs esq/e), frente/front, costas/back, gola/colar, punho, cós,
bolso/pocket, vista, forro — só se o usuário não tiver definido manualmente.

**Quantidade pelo nome do arquivo**: detecta `"5x"`/`"x5"` evitando confundir
com medida (`"30x40"` não é quantidade).

**Salvamento**: `POST /api/moldes` (novo) ou `PUT /api/moldes/{id}` (edição),
enviando todas as peças de todos os tamanhos de uma vez (substituição total).

---

## 9. Arte dentro do molde e Estampas

### 9.1 Modelo de ajuste (`public/arte-molde.js`)

```ts
AjusteArte {
  tipo: "arte" | "rapport"
  modo: "cobrir" | "caber" | "esticar"   // só relevante se tipo==="arte"
  escala: number   // %, default 100
  x, y: number      // cm — semântica difere por tipo!
  giro: number       // graus, múltiplo de 90
  ppcmArquivo: number|null  // pixels/cm do arquivo original (persistido, pois a imagem salva não guarda mais o dpi)
}
```

- **Arte**: entra uma vez, ajustada ao tamanho da peça pelo `modo`. `x/y`
  relativo ao **centro da peça**.
- **Rapport**: ladrilho no **tamanho real** (via `ppcmArquivo`), repetido via
  `CanvasPattern`. `modo` não se aplica. `x/y` define onde a repetição
  **começa**.

**Cálculo de encaixe da arte simples** (`encaixeDaArte`, em cm):
```
giro normalizado; deitada = giro em {90,270}; aW,aH = arte com giro aplicado
se modo=="esticar": w=alvoW, h=alvoH
senão:
  fator = modo=="caber" ? min(alvoW/aW,alvoH/aH) : max(alvoW/aW,alvoH/aH)
  w=aW*fator, h=aH*fator
w*=escala/100; h*=escala/100
x = (alvoW-w)/2 + ajuste.x
y = (alvoH-h)/2 + ajuste.y
```

**Cálculo de DPI seguro** (`ppcmDaArte`):
```
ppcmPedido = max(4, dpi/2.54)
ppcmTeto = sqrt(26_000_000 / (larguraCm*alturaCm))   // teto de 26 megapixels/peça
retorna min(ppcmPedido, ppcmTeto)
```

**Desenho final** (`desenharArteNoMolde`): canvas do tamanho da peça (cm×ppcm),
`clip("evenodd")` pelo contorno+furos (fill-rule par-ímpar vaza os furos),
desenha a arte ou o rapport, remove o clip, retorna PNG data-URL com
transparência fora do contorno — essencial para o encaixe reconhecer a
silhueta real e o PDF sair sem moldura branca.

### 9.2 Estrutura de "estampa"

```ts
Estampa {
  id, nome,
  pecas: [{ papel, arquivo, nomeOriginal, ajuste, url }]
}
```

Uma estampa é o "jogo de artes" de um molde — a arte da frente, das costas, da
manga, cada uma com seu ajuste — guardado por **papel de peça**, não por
tamanho. Isso é o que permite a mesma estampa servir P, M e G: ao trocar o
tamanho, o contorno muda e a arte se reajusta automaticamente ao novo contorno,
sem reenviar nada.

Múltiplas estampas do mesmo molde podem ser enviadas ao encaixe juntas num só
clique, cada uma com sua própria quantidade de "peças prontas".

---

## 10. Módulo Projetos

Hierarquia: **Cliente → Projeto → Peça**. UI tipo gaveta de arquivos.

Diferença crucial com Moldes: aqui **a arte já chega pronta e finalizada**
(estampa aplicada em outro programa) — não há passo de "encaixar arte no
contorno". A imagem enviada já é a peça final, incluindo o fundo (removido só
na hora de mandar para o Encaixe, nunca de forma permanente na biblioteca).

```ts
Peca (projeto) {
  id, nome, arquivo, url, miniatura,
  largura, altura,  // cm, do dpi do arquivo (pHYs do PNG / JFIF do JPEG), editável manualmente
  quantidade,        // qtd por unidade de produto (ex: manga entra 2×)
}
```

Miniaturas (240px) são geradas no upload e persistidas, para não decodificar a
arte de impressão inteira (dezenas de MP) só para desenhar um quadrado de
~57px na lista.

**Ajustes de encaixe associados ao projeto** (persistidos, não recalculados a
cada repetição): `larguraTecido, espaco, margem, giro`.

**Envio ao Encaixe**:
1. Salva o projeto.
2. Copia os 4 ajustes para os campos globais da tela de Encaixe.
3. Baixa e decodifica as imagens; **remove o fundo em lote** (mesma lógica de
   "olhar a borda inteira" usada na tela de Encaixe — ver §11 e a seção sobre
   detecção de fundo no README).
4. Multiplica `quantidade` de cada peça por `unidades` (quantas unidades do
   produto o usuário quer produzir): `qtd = round(peca.quantidade * unidades)`.

Apagar cliente/projeto remove as artes do disco (nunca uma que outro registro
ainda use — mesma regra da faxina, §6).

---

## 11. Motor de Encaixe (nesting) — núcleo do sistema

Esta é a parte algoritmicamente mais complexa e valiosa do sistema. Arquivos:
`encaixe.js`, `encaixe-motor.js`, `encaixe-mascara.js`, `encaixe-prepara.js`,
`encaixe-paralelo.js`, `encaixe-worker.js`, `prepara-worker.js`, `nfp.js`,
`encaixe-wasm.js`, `encaixe-giro.js`, `wasm/src/lib.rs`.

### 11.1 Pipeline geral

1. Cada arquivo/arte carregado vira uma **peça** (`largura`, `altura` cm, `qtd`,
   `giro`, `contorno`).
2. Ao mandar encaixar: peças expandem em **itens** (1 cópia cada); a **grade**
   (passo de célula, raio de folga) é calculada a partir da folga pedida.
3. Cada peça gera sua **silhueta** → **máscaras** (uma por rotação 0/90/180/270),
   cacheadas.
4. **Busca por receitas** (motor × agrupamento × ordem × heurística) roda em
   paralelo (Web Workers), repetindo tentativas até estabilizar ou esgotar o
   tempo.
5. Cada tentativa roda um **encaixador** (contorno/perfil, retângulo, faixas,
   ou NFP) sobre uma ordem embaralhada de "unidades" (peça solta ou bloco de
   2/3/4 cópias pré-combinadas).
6. O motor de contorno tem caminho rápido em **WebAssembly** (Rust).
7. O resultado alimenta o desenho do rolo e a geração de PDF.

### 11.2 Representação de dados

**Grade** (`grade(larguraTecido, espaco)`):
```
maisGrossa = clamp(larguraTecido/300, 0.2, 1)  // cm
maisFina = larguraTecido/1000
se espaco==0: passo=maisGrossa, raio=0
senão:
  metade = espaco/2
  partes = ceil(metade/maisGrossa); passo = metade/partes; raio = partes
  se passo < maisFina: passo=maisFina; raio=ceil(metade/passo)
folgaReal = raio*passo*2
```
`raio` = nº de células que a silhueta é dilatada para cada lado (a folga
"engorda" a peça, na grade, para nunca deixar peças mais perto do que a folga
pedida).

**Silhueta bruta** (`silhuetaDeDados`):
- `contorno=="caixa"`: bits todos 1.
- >2% de pixels transparentes: usa canal alfa (alpha≥40 → cheio).
- Senão: acha cor de fundo pela borda inteira (não 4 cantos!) e espalha
  flood-fill a partir dela; só aceita se o fundo for claro (média RGB≥200).
- Silhueta com <2% de células cheias é tratada como erro → cai para caixa.

**Engorde pela folga**: dilatação Manhattan separável (passada horizontal +
vertical), com moldura extra antes de dilatar (senão o engorde é cortado pela
borda da imagem).

**Máscara** (uma rotação):
```ts
Mascara {
  cols, rows: int
  topo, base: int[cols]     // relevo por coluna (primeira/última linha cheia; -1=vazia)
  desenho: byte[cols*rows]  // silhueta SEM folga (desenho real)
  cheio: byte[cols*rows]    // silhueta COM folga (para NFP)
  offX, offY: number        // cm, deslocamento do recorte até a imagem original
}
```
`topo`/`base` por coluna é a representação central — o "relevo" de uma peça
isolada, usado por todo o encaixador de contorno.

**Forma** (peça ou bloco posicionável):
```ts
Forma {
  cols, rows: int
  topo, base: int[cols]          // relevo combinado do bloco
  partes: [{item, mascara, rot, dcol, drow}]
  nCols, somaTopo, maxBase: number
}
```

**Relevo do tecido**: `perfil: int[colsTecido]` — altura já ocupada em cada
coluna do rolo.

### 11.3 Encaixador por contorno (bottom-left sobre o relevo)

> **Tentativa de melhoria além do original (02/09/2026) — top-K candidatos +
> look-ahead, testada e REVERTIDA.** A pedido explícito ("preciso que o
> encaixe seja o melhor resultado possível"), implementamos uma ideia do
> `docs/guia-melhorias-aproveitamento-encaixe.txt` que o JS original não tem:
> guardar as K=3 melhores posições por unidade (`EncaixadorPorContorno.TopKPosicoes`,
> Core — ainda existe, testada, só não é mais chamada por
> `EncaixeService`) e, entre elas, escolher a que deixasse a PRÓXIMA unidade
> mais barata (look-ahead de 1 passo, §3.3 do guia), em vez de só a
> imediatamente melhor. Medido em lote real (57 peças): testamos três
> variações — look-ahead sempre, com margem de empate proporcional
> (`P1/10`), com margem de empate apertada (`P1±1`) — e nenhuma rendeu
> ganho de aproveitamento que compensasse a perda de tentativas (caía de
> ~500 pra ~150-250 em todas as variações; nas rodadas com margem apertada,
> "Retângulo" passou a vencer as 3 rodadas seguidas, sinal de que "Contorno"
> ficou MENOS competitivo, não mais). Conclusão: nesse formato de peças
> (garment, muitas com tamanho parecido), o custo extra do top-K por si só
> (juntar/ordenar candidatos de várias formas/rotações) já supera o ganho do
> look-ahead. Revertido pro `MelhorPosicaoDaUnidade` simples de sempre — ver
> `EncaixeService.ExecutarContorno` no código atual. Fica documentado aqui
> pra não reintroduzir a mesma ideia sem antes ter uma resposta pro "por que
> não rendeu" (guia §19, checklist: "o ganho vem de melhor busca ou só de
> mais tempo?").

> **Podas de performance portadas (02/09/2026)** — as duas otimizações que a
> nota abaixo já documentava como deliberadamente OMITIDAS ("Adicionar
> depois se a performance real exigir") foram portadas: poda por
> colunas-sonda + corte precoce (só heurística "fundo", matematicamente
> seguro — nunca muda qual posição vence) e varredura "pulando de 3 em 3"
> (aproximação deliberada, igual ao original). Motivo concreto: usuário
> rodou o mesmo lote real (25 arquivos/57 peças) com a MESMA configuração
> nos dois sistemas (179cm, 4mm, margem 0, 60s) e reportou o nosso saindo
> pior — 5,83m contra 5,82m do original. Investigando, o problema não era
> qualidade de busca: eram **~4.700-5.200 tentativas totais em 60s contra
> ~129.000 do original** (~27x menos), porque o motor de contorno sozinho
> fazia só ~36 tentativas/s (contra a ordem de milhares/s do motor de
> retângulo, que ignora silhueta e por isso é muito mais barato por
> natureza) — "Retângulo" vencia por vantagem de tentativas, não por ser
> genuinamente melhor pra essas peças. Duas causas achadas e corrigidas:
> (1) a varredura de posição em si não tinha NENHUMA poda (documentado
> abaixo, agora implementado); (2) `AgrupamentoDeBlocos.FormasDoBloco` (a
> geometria de bloco dupla/trio, que já faz sua própria busca cara via
> `EncostoDeFormas.EncostarNaForma`) era recomputada por peça-base EM TODA
> TENTATIVA em vez de uma vez só por busca — o mesmo tipo de bug já achado e
> corrigido antes pra "cruzada" (§11.8), só que nunca replicado pra
> dupla/trio. Corrigido com o mesmo padrão: `EncaixeService` agora
> pré-computa `candidatosDeBlocoPorPeca` uma vez por busca inteira (ver
> `PrecomputarCandidatosDeBloco`), do jeito que já fazia com
> `PrecomputarCandidatosCruzados`. **Medido** (mesmo lote real, mesma
> config exata do usuário, 3 confirmações): motor de contorno sozinho subiu
> de ~36 pra ~152 tentativas/s (4,2x); a busca completa (automático, os dois
> motores competindo) subiu de ~4.700-5.200 pra **~25.000 tentativas em
> 60s** (~5x) — e pela primeira vez "Contorno·Trio" passou a VENCER a
> disputa (antes sempre perdia pra "Retângulo" só por falta de fôlego de
> busca), com o mesmo consumo final (~5,83m) que "Retângulo" já alcançava
> sozinho. Ainda fica ~5x atrás do throughput agregado do original (~400/s
> contra ~2.090/s) — gap real, não fechado por completo nesta rodada;
> possíveis próximos passos (não feitos ainda): eliminar alocações de lista
> por tentativa no laço externo, pool de arrays pro `perfil`.
>
> **Tentativa seguinte (02/09/2026) — reduzir alocação por tentativa,
> ganho pequeno.** Pool de array (`ArrayPool<int>` pro `perfil`, evita
> `new int[colsTecido]` a cada tentativa), modo "leve" (`detalhado:false`
> na busca — pula `List<ItemDeResultado>`/área real/ids não-encaixados,
> que a busca nem lê, só compara `ConsumoCm`; só a ordem VENCEDORA é
> reconstruída em detalhe no final) e removida a cadeia LINQ
> (`Where`/`Select`/`Where`) do caminho de peça solta. **Medido** (mesmo
> lote real, mesma config): motor de contorno sozinho foi de ~152 pra
> ~162 tentativas/s (+7%); busca completa ficou praticamente igual
> (~25.000 tentativas em 60s, mesmo patamar de antes). Conclusão: alocação
> NÃO é o gargalo restante — o custo é mesmo o loop de pontuação em si
> (`MelhorPosicaoDaUnidade`, O(colsTecido/salto × formaColunas) mesmo com
> as podas), que domina o tempo por tentativa independente de quanto GC
> pressiona. Mantido porque não piora nada e é estritamente mais barato,
> mas não é onde o próximo ganho grande vai vir — próxima hipótese a testar
> seria reduzir o CUSTO do próprio loop (ex.: representar `perfil`/`Topo`
> em blocos de 64 colunas com operações bit a bit, ou paralelizar a
> varredura de x dentro de uma única tentativa) em vez de reduzir alocação.
>
> **`MelhorPosicaoDaUnidadeV2` — SIMD, throughput real, mas não fecha o gap
> contra o concorrente (02/09/2026).** Motivado por um pedido de negócio
> concreto: cliente comparou com o Audace (concorrente) num lote de
> produção real (4 arquivos/20 peças, 179cm/4mm/margem 0) e ele conseguiu
> 2,46m contra os ~2,50m nossos (depois do bug de reconstrução corrigido,
> ver §11.7) — usuário confirmou que essa diferença pesa na decisão de
> clientes trocarem de sistema. Medido primeiro que RODAR MAIS TEMPO não
> ajudava (5min real deu o MESMO 2,504m que 60s já achava — busca
> convergida, não é questão de tempo). Daí a hipótese: baratear o CUSTO do
> loop, não só podar quantas vezes ele roda.
>
> Implementado como V2 ISOLADA (a pedido explícito: "crie uma v2... assim
> evita desfazer caso não tenha sucesso, apenas apaga o método") — a V1
> (`MelhorPosicaoDaUnidade`) nunca foi editada, só um novo método adicional
> no mesmo arquivo. `MelhorPosicaoDaUnidadeV2` processa `Vector<int>.Count`
> colunas de uma vez via SIMD real do hardware (`System.Numerics.Vector`,
> 8 `int32` por instrução em CPU com AVX2) em vez de uma coluna por
> iteração — mesma fórmula de pontuação, colunas inválidas (`Topo<0`)
> mascaradas com `Vector.ConditionalSelect` em vez de puladas uma a uma.
> Troca consciente: perde o "corte precoce" (poda 2, poda 1 e a varredura
> "pulando de 3" continuam) — não dá pra abortar no meio de um bloco de 8
> colunas sem complicar o mascaramento, então processa o bloco inteiro
> sempre.
>
> **Corretude primeiro, sempre**: 700+ combinações aleatórias de
> forma/perfil/colsTecido/heurística/salto comparando V1×V2 bit a bit
> (`EncaixadorPorContornoV2Tests`, 7 testes, TODAS batendo exato — mesmo
> X/Y/P1/P2) antes de sequer medir performance.
>
> **Medido** (mesmo lote real, 179cm/4mm/margem 0):
> | | 60s | 5min |
> |---|---|---|
> | V1 | ~150-170k tentativas → 2,50-2,53m | 659k tentativas → 2,504m |
> | V2 | **431k tentativas** (2,5-4x mais) → 2,514m | **2,12 MILHÕES** de tentativas (3,2x mais que V1) → 2,502m |
>
> Throughput real e comprovado (2,5-4x mais tentativas por segundo,
> resultado bit-idêntico à V1 — puro ganho, sem risco de regressão de
> qualidade). MAS: mesmo com 2,12 milhões de tentativas em 5 minutos, o
> resultado não passou de ~2,50m — o MESMO platô que V1 já achava com
> muito menos tentativas. **Conclusão honesta**: o gap de ~1,7% contra o
> concorrente (2,50m vs 2,46m) não é "falta de tentativas" nem "loop caro
> demais" — é o TETO do espaço de busca atual (as receitas/agrupamentos/
> heurísticas disponíveis já exploraram o que dava pra explorar nesse
> formato de peça). Fechar esse gap de verdade vai precisar de um
> algoritmo/heurística GENUINAMENTE diferente (busca local real tipo
> ruin-and-recreate bem feito — não o `ReconstruirRabo` já testado e
> revertido acima —, ou uma heurística de posicionamento nova), não de
> rodar mais rápido o que já existe. Mantido em produção (é estritamente
> melhor que V1 em throughput, sem contrapartida), mas documentado aqui
> pra não reprisar "só preciso rodar mais rápido" como próxima hipótese —
> já foi medido e não é isso.

> **`HeuristicaDeContorno.Contato` — heurística nova, testada e REVERTIDA
> do padrão (02/09/2026).** Consequência direta da conclusão acima ("precisa
> de heurística genuinamente diferente, não mais velocidade"): em vez de
> julgar uma posição só pela altura (`Fundo`) ou pelo buraco morto acima da
> forma (`Vazio`), esta pontua por QUANTAS colunas da forma ficam de
> verdade encostadas no relevo já ocupado (sem gap) — técnica clássica de
> nesting ("perímetro de contato"), tentando "grudar" formas côncavas
> (cava com cava, gola com gola) melhor que bottom-left puro. Implementada
> como método isolado (`MelhorPosicaoPorContato`, roteado a partir de
> `MelhorPosicaoDaUnidade` quando a heurística é `Contato`; a V2/SIMD
> delega de volta pra V1 nesse caso — não precisa reimplementar tudo em
> SIMD só pra testar a ideia) e coberta por testes calculados à mão antes
> de medir contra dados reais.
>
> **Medido** (mesmo lote real do concorrente, 179cm/4mm/margem 0, 60s, 3
> rodadas, com `Contato` competindo automaticamente junto de `Fundo`/`Vazio`):
> piorou de 2,502-2,514m pra **2,518-2,528m**, e `Contato` não venceu
> NENHUMA das 3 rodadas — quem sempre ganhou foi `Fundo`. Causa provável:
> adicionar uma 3ª heurística aumenta 50% o número de receitas da família
> contorno (24→36), diluindo o orçamento de busca por receita sem
> contrapartida real — o mesmo padrão de "mais opções competindo nem
> sempre ajuda" já visto com o top-K/look-ahead (§11.3). Revertida do
> padrão (`GeradorDeReceitas.HeuristicasDeContorno` volta a só
> `[Fundo, Vazio]`); `HeuristicaDeContorno.Contato` continua no Core,
> testada, disponível se uma hipótese melhor de COMO usá-la aparecer (ex.:
> só pra formas com muita concavidade, não pra todo agrupamento igual).

Núcleo do sistema: `melhorPosicaoDaUnidade(perfil, colsTecido, unidade,
heuristica, salto)`.

**Ideia**: para cada `x` candidato, a unidade "desce" até a primeira coluna
encostar: `y = max_c(perfil[x+c] - topo[c])` sobre colunas válidas. Nunca
sobrepõe, porque `perfil` guarda o ponto mais baixo já ocupado em cada coluna.

**Avaliação de uma posição x** (pseudocódigo):
```
notaCom(y) = usaVazio ? y*nCols + somaTopo - janela : y + maxBase + 1

// poda barata: só nas colunas-sonda (até 8)
se melhor!=null e podeCortar:
  piso = max_i(perfil[x+sondas[i]] - topo[sondas[i]])
  se notaCom(piso) > melhor.p1: descarta sem medir tudo

// medida exata, coluna a coluna
y=0; somaPerfil=0
para c em 0..cols-1:
  se topo[c]<0: continue
  altura=perfil[x+c]; somaPerfil+=altura
  encosta=altura-topo[c]
  se encosta<=y: continue
  y=encosta
  se melhor!=null e podeCortar e notaCom(y)>melhor.p1: descarta (corte precoce, y só cresce)

vazio = y*nCols + somaTopo - somaPerfil
fundo = y + maxBase + 1
(p1,p2) = usaVazio ? (vazio,fundo) : (fundo,vazio)
se (p1,p2) < melhor: atualiza melhor
```

Duas heurísticas: **"fundo"** (minimiza `y+maxBase+1`, mais alto possível) e
**"vazio"** (minimiza área de buraco morto acima da forma).

**Varredura exata vs "pulando de 3 em 3"**:
```
se pulo<=1: testa todo x
senão: passada grossa de `pulo` em `pulo` + extremo final + passada fina (±pulo-1) ao redor do melhor local desta forma
```
Medido: pulo=3 é o ponto ótimo (2,5-2,8× mais tentativas pelo mesmo custo).

**Laço externo**:
```
zerar perfil
para cada unidade (na ordem dada):
  escolha = melhorPosicaoDaUnidade(perfil, colsTecido, unidade, heuristica, saltoX)
  se não achou: unidade inteira vai para naoEncaixadas
  senão: assentarUnidade (marca perfil[x+c]=y+base[c]+1 por coluna)
retorna {posicoes, naoEncaixadas, consumo: fundoMax*passo+margem*2, areaReal}
```

### 11.4 Encaixador por caixa (bounding box / MaxRects)

Mantém lista de retângulos livres do rolo; escolhe o melhor segundo heurística
(`bl`=Bottom-Left, `bssf`=Best-Short-Side-Fit, `blsf`=Best-Long-Side-Fit,
`baf`=Best-Area-Fit), recorta o espaço usado, remove sobreposições redundantes.
Ignora a silhueta (trata peça como caixa cheia) ao decidir posição, mas ainda
contabiliza área real para o % de aproveitamento.

Dois agrupamentos: `"empe"` (giro forçado fixo) e `"deitada"` (permite trocar
largura↔altura, só existe se alguma peça tem `giro=="livre"`).

### 11.5 Encaixador por NFP (No-Fit Polygon)

Técnica usada por programas profissionais. `nfp.js`, 812 linhas.

**Preparo de uma peça** (`pecaEmPoligonos`):
1. Engorda a silhueta com folga extra (`ceil(tolerancia/passo)` células).
2. Extrai contorno vetorial por marching-squares andando pelas quinas (16
   estados de vizinhança, desempate de "sela" pela direção anterior).
3. Simplifica (Douglas-Peucker, tolerância=passo) + remove pontos colineares.
4. **Decompõe em polígonos convexos** (Hertel-Mehlhorn: ear-clipping + fusão de
   triângulos vizinhos enquanto continuar convexo).
5. Se >16 pedaços, cai para o casco convexo inteiro (aproximação mais folgada).

**NFP entre dois convexos** (soma de Minkowski):
```
nfpConvexo(paradaA, movelB):
  bInvertido = antiHorário(antiHorário(movelB).map(p=>{-p.x,-p.y}))
  retorna somaDeMinkowski(antiHorário(paradaA), bInvertido)

somaDeMinkowski(a,b):
  lados = todos vetores-lado de a e b, ordenados por ângulo
  percorre lados ordenados a partir de cantoDeBaixo(a)+cantoDeBaixo(b), acumulando
```

Índice espacial (grade 48×48) sobre a bbox dos NFPs de cada par de peças, para
teste de invasão O(1) em vez de O(n).

**Posicionamento** (`encaixarPorNFP`): para cada item, gera candidatas
(extremos da faixa, vértices dos NFPs das últimas 25 peças colocadas,
interseções com bordas do tecido, interseções entre lados de NFPs das últimas
4 peças), ordena por `(y,x)`, testa em ordem e usa a primeira sem invasão.

NFP **perde** do encaixe por perfil na maioria dos casos (só ganha ~10% em
lotes de peça única) — por isso roda numa única fatia paralela dedicada (§11.7).

> **Atualização (02/09/2026) — despachado.** A matemática pura já estava
> portada e testada (`CalculadoraDeNfp` via Clipper2, `EncaixadorPorNfp`, 19
> testes) desde antes, mas nunca fora chamada por `EncaixeService` —
> `ExecutarReceita` lançava `NotSupportedException` pra `MotorDeEncaixe.Nfp`.
> Agora `Receita.DeNfp()` entra na lista padrão junto do grupo de contorno
> (fora quando o modo é "sempre caixa") e o dispatcher chama
> `EncaixadorPorNfp.Encaixar` de verdade.
>
> **Regra de segurança (guia de melhorias §7).** O NFP só valida por
> matemática de polígono contínuo — o guia documenta bug histórico real do
> motor original com silhuetas de múltiplos componentes. Por isso toda
> posição que sai do NFP passa por uma SEGUNDA checagem, de natureza
> DIFERENTE e independente: `ValidadorDeSobreposicaoNfp`, que rasteriza cada
> peça posicionada na mesma grade discreta usada pelo motor de contorno há
> meses e detecta qualquer colisão célula a célula. Se a checagem discreta
> achar sobreposição (mesmo que rara), a tentativa inteira é desqualificada
> (consumo infinito, nada assentado) — nunca arrisca devolver posições
> sobrepostas pra vencer a disputa.
>
> **Bug de performance achado e corrigido antes de medir.** A primeira
> tentativa real (25 arquivos/57 peças, benchmark de produção) estourou de
> 15s pra **17,7 minutos** numa única busca. Causa: `LeitorDeImagemDeEncaixe`
> já simplifica o contorno traçado da imagem, mas em **espaço de pixel**,
> antes da conversão pra cm — na escala real isso deixa ~194 vértices por
> peça em média (até 361 nas mangas), praticamente sem corte nenhum. A soma
> de Minkowski do NFP é ~quadrática no número de vértices, e o motor
> recalcula contra TODAS as peças já colocadas a cada novo item (O(n²) pares)
> — em 57 itens isso multiplicou o custo de cada par por dezenas de milhares.
> Corrigido resimplificando o contorno especificamente pro NFP, já em cm,
> com tolerância maior (1,5cm — desvio de área medido <1,1%, ~9 vértices por
> peça em média): o NFP sozinho caiu de minutos pra ~180ms nos 57 itens
> reais. A checagem de segurança e o resultado final continuam usando o
> contorno ORIGINAL (não a versão resimplificada) — o pior caso de uma
> resimplificação imprecisa é perder a disputa por desqualificação, nunca
> uma sobreposição de verdade chegando ao resultado.
>
> **Medido** (mesmo benchmark de 25 arquivos/57 peças, 3 rodadas após a
> correção): busca completa em ~17,5s (dentro do orçamento de 15s + folga
> normal da última tentativa), 0 crashes, 0 peças não encaixadas, 0
> sobreposições. NFP participa da disputa em toda busca (confirmado via
> placar de memória) mas não venceu em nenhuma das 3 rodadas — quem venceu
> foi sempre `Contorno·Cruzada` (74,4–77,2% de aproveitamento). Consistente
> com a nota do original acima ("NFP perde na maioria dos casos"): o motor
> agora está corretamente disponível e seguro, sem travar a busca, mesmo
> quando não é ele quem vence.
>
> **Giro portado (02/09/2026) — item 2 do plano de melhoria do encaixe.**
> Até aqui o NFP só rodava na orientação 0° da peça, mesmo quando o giro
> configurado (`TipoDeGiro`) permitia 180°/livre — uma limitação real, já
> que "mantém sentido" já libera 180° pra praticamente toda peça de roupa
> (a cópia de cabeça pra baixo). `ItemParaNfp` passou de um único
> `Contorno` pra `ContornosPorRotacao` (um contorno vetorial por rotação
> permitida, girado com o novo `Geometria.Rotacionar90` — mesma convenção
> de `EncaixeViewModel.MontarGeometriaRotacionada`, portada pro Core pra
> não depender de UI). `EncaixadorPorNfp.Encaixar` testa CADA rotação
> disponível pra cada item (recalculando o NFP contra as peças já
> colocadas em cada uma) e fica com a melhor posição entre todas —
> mesmo critério de desempate de sempre (mais baixo, depois mais à
> esquerda). Custo: até 4x mais caro por item em giro "livre" (uma
> rodada de NFP inteira por rotação testada) — aceitável porque o NFP já
> tinha folga de sobra depois da correção de performance acima (~180ms
> pros 57 itens reais). A checagem de segurança e o resultado final
> (`EncaixeService.ExecutarNfp`) usam o contorno ORIGINAL girado na
> rotação que venceu, não a versão resimplificada nem a rotação 0
> hardcoded. **Medido** (mesmo benchmark real, 3 rodadas): 0 crashes, 0
> sobreposições, `Contorno·Cruzada` continuou vencendo (NFP ainda não
> venceu nenhuma rodada nesse lote) — a mudança abre a capacidade sem
> regressão, mas neste formato específico de peça (garment, giro
> "mantém sentido" já testava 180° mesmo antes por natureza — a novidade
> real é habilitar 90°/270° pra peças com giro "livre") o motor vencedor
> continua sendo contorno.

### 11.6 Encaixador por faixas (strip packing)

Divide o rolo em 2 colunas verticais, roda o encaixador de contorno
independentemente em cada uma. Cortes candidatos vêm das larguras das próprias
peças (testando múltiplos como corte), só aceitos se a faixa restante ainda
comporta a peça mais estreita.

**Medido como perdedor** na maioria dos casos (~5-7% pior) — fica fora da
lista padrão de motores, disponível só por configuração explícita.

### 11.7 Sistema de receitas e busca (`buscarMelhorEncaixe`)

**Dimensões de uma receita**:
```
Receita { motor, agrupamento, ordem, heuristica, corte? }
motor: contorno | retangulo | faixas | nfp
agrupamento (contorno/faixas): solta | dupla | trio | quarteto  (padrão: [dupla,solta,trio])
ordem: area | altura | lado (+ largura para retângulo/NFP)
heurística: fundo|vazio (contorno) — bl|bssf|blsf|baf (retângulo) — fixa "encosta" (NFP)
```

**Loop principal** (pseudocódigo):
```
1) PASSADA BASE: cada receita 1x, sem embaralhar, ordenada por peso da memória
2) MELHORIA (até esgotar tempo ou parar manualmente):
   perseguindo = existe alvo (recorde de trabalhos parecidos) e ainda não alcançado e <60% do tempo
   parede (sem ganho há msSemGanho e !perseguindo): alterna modo refinar/explorar, suspende poda
   naRoda = poda ativa? receitas até 6% acima do melhor : todas
   sorteia receita (15% de chance ignora a poda e usa lista inteira)
   roda tentativa com ordem embaralhada, compara com melhor
```

**Poda** (`receitasNaRoda`): `PODA_TOLERANCIA=1.06` (6% de tolerância acima do
melhor), `PODA_MINIMO=4` (nunca poda abaixo disso), `PODA_FRESTA=0.15` (15% dos
sorteios ignoram a poda). Receita nunca testada não é podada. O placar/histórico
**não** enviesa o sorteio das tentativas (medido: piorava) — só ordena a
passada base.

**Perseguir recorde**: desativa a checagem de "sem ganho, desiste" mas mantém a
poda ativa.

**Refinar vs explorar**: "explorar" parte da lista crua com embaralhar forte;
"refinar" parte da melhor ordem já encontrada com embaralhar leve. Alternam a
cada parede.

> **Tentativa de melhoria (02/09/2026) — "ruin and recreate" focado no rabo
> da ordem, testado e REVERTIDO.** Item 3 de um plano de 3 melhorias pra
> reduzir consumo/aumentar aproveitamento (itens 1 e 2: performance do
> motor de contorno e giro no NFP, ambos medidos e mantidos — ver §11.3 e
> §11.5). A ideia: em vez de `Embaralhamento.Leve` trocar posições
> ALEATÓRIAS em qualquer lugar da melhor ordem já achada (podendo mexer
> numa parte que já estava bem encaixada), `Embaralhamento.ReconstruirRabo`
> (novo, ainda no Core, testado) mantém a "cabeça" da ordem INTACTA e só
> embaralha os últimos ~20% — no motor bottom-left, é ali que costuma estar
> o que define o fundo mais alto (o consumo final), então teoricamente uma
> reordenação ali tem mais chance de render. **Medido** (mesmo lote real,
> mesma config exata, 3 rodadas de cada, comparação A/B isolada — sem
> nenhuma outra busca rodando antes na mesma sessão pra não confundir
> JIT/GC): `ReconstruirRabo` deu 5,83m/81,2% em 3/3 rodadas;
> `Embaralhamento.Leve` (o de sempre) deu 5,83m/81,2% em 2/3 e 5,88m/80,5%
> na terceira — dentro da variação normal de uma busca estocástica, sem
> diferença real atribuível à troca. (Uma medição isolada assim também
> corrigiu uma leitura enganosa: uma rodada anterior, que media
> "SempreContorno" e depois "Automático" na MESMA sessão de processo, tinha
> pela metade dos consumidos tentativas — artefato de medição, não do
> algoritmo.) Revertido pra `Leve` (padrão de sempre);
> `Embaralhamento.ReconstruirRabo` continua no Core, testada (4 testes),
> disponível se algum dia aparecer evidência real de que ajuda — mesma
> política do top-K/look-ahead (§11.3): sem ganho medido, não vira padrão.

> **BUG REAL achado e corrigido (02/09/2026) — "melhor ordem" dessincronizava
> do "melhor consumo".** Achado pelo usuário: a tela mostrava "melhor até
> agora" caindo pra um valor durante a busca, mas o resultado final sempre
> saía PIOR que aquele número — nunca batia. Reproduzido isolado (script
> fora da UI, comparando o mínimo visto via `IProgress<AndamentoDoEncaixe>`
> contra `ResultadoDeEncaixe.ConsumoCm` final): divergência confirmada em
> praticamente toda busca real, de ~1 a ~3cm.
>
> **Causa**: em `RodarTentativa`, quando a MESMA receita que já é a melhor
> global melhora AINDA MAIS (acha uma ordem melhor pra si mesma — comum no
> modo "Refinar", que insiste na receita vencedora), `placar` é o MESMO
> OBJETO já apontado por `melhorGlobal` (mesma referência mutável). A
> sequência era: `placar.Registrar(novoConsumo, novaOrdem)` — que JÁ MUTAVA
> `melhorGlobal.MelhorConsumoCm` pro valor novo — e SÓ DEPOIS a comparação
> `resultado.ConsumoCm < melhorGlobal.MelhorConsumoCm` rodava, virando uma
> AUTO-COMPARAÇÃO (`novoValor < novoValor`, sempre falsa). O "if" nunca
> disparava nesse caso específico, então `melhorOrdemGlobal` (variável
> SEPARADA do placar) ficava PRESA numa tentativa ANTERIOR — pior — da
> MESMA receita, enquanto `melhorGlobal.MelhorConsumoCm` (lido depois,
> direto do placar) já refletia corretamente o valor novo. No fim da busca,
> `EncaixeService` reconstrói o resultado com esse par (receita, ordem) —
> e como a ordem estava errada, a reconstrução nunca reproduzia o consumo
> relatado como "melhor".
>
> **Achado só agora, apesar de já existir desde antes desta sessão**,
> porque toda medição anterior deste documento só conferia o
> `ConsumoCm`/`Tentativas` FINAIS — nunca cruzava contra o traço de
> progresso ao vivo. Isso quer dizer que TODAS as medições anteriores deste
> arquivo (§11.3, §11.5, §11.8, este próprio §11.7) foram feitas com esse
> bug presente — mas como ele afeta a reconstrução final de QUALQUER busca
> igualmente, comparações A/B RELATIVAS entre duas variantes (que é o que
> foi medido em cada caso) continuam válidas; só o valor ABSOLUTO de
> "melhor resultado possível" estava sistematicamente pior do que a busca
> já vinha de fato alcançando.
>
> **Corrigido**: captura o `MelhorConsumoCm` global ANTES de chamar
> `placar.Registrar(...)`, comparando contra esse valor "congelado" em vez
> de ler o placar (potencialmente já mutado) depois. Teste de regressão
> dedicado (`BuscarMelhorEncaixe_MesmaReceitaSeAutoSuperandoVariasVezes_MelhorOrdemAcompanhaOMelhorConsumoCm`,
> Core.Tests) — confirmado que FALHA no código antigo (ordem presa na
> primeira tentativa em vez da última/melhor) e PASSA com a correção.
>
> **Medido** (lote real de um pedido de produção, 4 arquivos/20 peças,
> 179cm/4mm/margem 0/60s, 3 rodadas): divergência sumiu 100% (progresso e
> resultado final batendo exatamente nas 3 rodadas) — e o valor absoluto
> melhorou de ~254-257cm pra **251,6-252,8cm**, mais perto ainda do
> concorrente (2,46m/246cm nesse mesmo lote). Esse bug sozinho custava
> tecido de verdade, silenciosamente, em praticamente toda busca — a
> correção mais valiosa desta sessão em termos de impacto direto no
> "coração da aplicação".

### 11.8 Agrupamento em blocos (dupla/trio/quarteto)

> **Atualização (01/09/2026) — despachado.** A matemática pura
> (`AgrupamentoDeBlocos`/`EncostoDeFormas`/`Forma.Partes`, Core) já estava
> portada e testada, mas não era usada em busca nenhuma — `GeradorDeReceitas`
> só gerava agrupamento "Solta". Descoberto ao comparar resultados reais com a
> versão Tauri (aproveitamento visivelmente maior lá, com peças em pares
> interligados) e confirmado lendo `AGRUPAMENTOS_PADRAO = ["dupla", "solta",
> "trio", "cruzada"]` no `encaixe-motor.js` real — dupla vem antes até de
> solta na lista padrão. Agora `GeradorDeReceitas` gera receitas
> "dupla"/"trio" pro motor contorno, e `EncaixeService.ExecutarContorno`
> agrupa cópias da mesma peça-base em blocos (`MontarUnidades`), tenta as
> formas candidatas do bloco (`CandidatosDaUnidade`) e desmembra o resultado
> de volta em posições individuais por peça ao assentar (`Assentar`) — usando
> `ParteDaForma.RotacaoGraus` explícito, não comparação de referência entre
> máscaras (que falha: `RotacaoDeMascara.Rotacionar(m, 0)` devolve a mesma
> instância, mas 90/180/270 sempre alocam objeto novo — duas rotações
> "iguais" de origens de chamada diferentes nunca são o mesmo objeto). Medido
> num lote real de 57 peças de camiseta: 7,23 m → 7,05 m, com "trio" vencendo
> — bate com a nota de medição do JS logo abaixo. **"Cruzada" também portada**
> (mesmo dia, a pedido explícito: "preciso que o encaixe seja o melhor
> resultado possível") — `AgrupamentoCruzado.Tentar` (Core) junta peças de
> formatos DIFERENTES; `EncaixeService.PrecomputarCandidatosCruzados` mede a
> economia de cada PAR de formatos e ordena guloso, do que mais economiza pro
> que menos, **uma vez por busca inteira** (não por tentativa — o pareamento
> não depende da ordem sorteada, só a sequência de visita das unidades
> resultantes depende; recalcular a cada tentativa fazia as tentativas totais
> despencarem de ~500 pra ~150 num lote real, sem ganho nenhum em troca — o
> pareamento dá sempre o mesmo resultado). `MontarUnidadesCruzadas` consome
> esse pareamento pronto a cada tentativa, só decidindo a ordem de visita.

Dupla junta a peça com a cópia invertida (180°) — "manga com manga invertida
fecha quase um retângulo". Trio/quarteto estendem a ideia (a "tira" do
marcador de confecção).

```
formasDoBloco(copias, tamanho):
  para rotInicial em {0,180}:
    bloco = forma da 1ª cópia
    para k=1..tamanho-1:
      testa encostar a k-ésima cópia em rot 0 e 180, escolhe a que dá menor área
      se nenhuma encaixa: falha
  uteis = arranjos com area < área_solta*tamanho*0.98
```

`encostarNaForma`: testa todo deslocamento horizontal possível da peça nova
relativa ao bloco, escolhe o que produz o menor retângulo envolvente.

Medido: trio entra como receita adicional (não substitui dupla/solta), ganho
~1,75-2,22% menos tecido somando com poda. Quarteto ficou de fora por não
compensar o custo de mais uma receita na passada base.

### 11.9 Paralelização (Web Workers → Task/Parallel em .NET)

```
n = clamp(hardwareConcurrency-1, 1, 8)   // 1 núcleo reservado à UI
fatia k recebe receitas de índice k, k+n, k+2n, ... (round-robin)
2 primeiras fatias (k=0,1): varredura exata (pulo=1) — "piso" de qualidade
demais fatias: pulo=3
```

**Fatia reservada para NFP**: no modo automático (contorno+retângulo
disputando) com ≥3 workers, a **última fatia** roda exclusivamente NFP.

Em .NET: substituir por `Task.Run`/`Parallel.For` — sem a complexidade de
postMessage/memória isolada, pode compartilhar memória diretamente (com
sincronização adequada).

### 11.10 Módulo WASM (Rust) → pode virar C# puro

`wasm/src/lib.rs` — o laço quente (`melhorPosicaoDaUnidade`/`encaixarContorno`)
portado para Rust/WASM. Interface: **uma vez por busca** atravessam as
**formas** (topo/base/sondas/etc.); **a cada tentativa** atravessa só a
**ordem** (índice por unidade); volta `(formaVencedora, x, y, fundo)` por
unidade. O relevo (`perfil`) nasce e morre inteiramente dentro do WASM a cada
chamada.

Ganho medido: ~3,9× mais tentativas no mesmo tempo. Em .NET, não é necessário
WebAssembly — a mesma lógica pode ser escrita em C# com `Span<int>`/`unsafe`
para performance equivalente, já que não há fronteira JS↔nativo a atravessar.

Único export do Rust: `encaixar(cabecalho: *const i32) -> i32` (retorna
`fundoMax`). Fallback: se WASM não carrega, cai no caminho JS puro — em .NET
isso simplesmente não existe (é tudo C# nativo).

### 11.11 Giro/rotação (`encaixe-giro.js`)

```
ROTACOES_POR_GIRO = {
  "180":  [0, 180],    // padrão — mantém sentido do fio
  "fixa": [0],          // nunca gira
  "livre":[0,90,180,270] // malha lisa, sem sentido
}
```

### 11.12 Memória de aprendizado

**Assinatura do trabalho** (calculada no frontend, antes de mandar ao
servidor):
```
assinaturaDoTrabalho(pecas, larguraTecido):
  formatos = pecas.map(p => `${round(ocupacao*10)}:${round(log2(largura/altura)*2)}`).sort()
  retorna `l${round(larguraTecido/10)}|${formatos.join(",")}`
```
Agrupa trabalhos por **forma** (ocupação da caixa + proporção), não por
nome/quantidade — trabalhos diferentes com peças parecidas compartilham
aprendizado.

**Uso**: `melhorAntes` (recorde do tipo) vira `alvo` — enquanto não alcançado e
dentro dos primeiros 60% do tempo, a busca "persegue" (não desiste por falta de
ganho, mas mantém a poda).

**Encaixe guardado exato**: chave diferente da assinatura — identifica o
trabalho **exato** (peças+quantidades+giro+contorno+largura+folga+margem, com
hash FNV-1a). Serve para reoferecer o melhor encaixe já obtido, não influencia
a busca em si.

---

## 12. Memória de aprendizado do Encaixe

> **Atualização (01/09/2026) — investigado no código-fonte real e portado.** O
> que a §12.1 abaixo descreve foi lido direto de `encaixe-rede.js`,
> `encaixe-memoria.js` e `encaixe-motor.js` no snapshot atual do projeto
> original (`C:\projetos\optmize-full`, fora deste repositório), e já está
> implementado no port .NET — `OptimizePro.Services.Encaixe.EncaixeMemoriaService`
> implementa a fórmula de duas camadas desta seção (§12) **e** a rede neural de
> §12.1, como terceira camada opcional por cima, não uma substituição.
> Confirmado por leitura do código: a fórmula de duas camadas **não mudou** no
> projeto original — o port .NET já estava fiel a ela antes mesmo da rede.

(Detalhes de schema já cobertos em §3.5/3.6/3.10/3.10.1; contrato de API em §4.4.)

Duas camadas combinadas no GET `/memoria`:
```
memoria[receita] = { usos: usosGeral + usosDoTipo*2, vitorias: vitoriasGeral*0.4 + vitoriasDoTipo*2 }
```
Camada geral (todas as assinaturas) pesa menos (0.4×); camada do tipo exato
pesa mais (2×) — com poucos encaixes daquele tipo, a camada geral sustenta o
palpite; com histórico próprio acumulado, ele passa a dominar.

`encaixe_guardados`: guarda o encaixe **físico completo** (posição de cada
peça), não só a metragem — porque a busca é sorteada e um resultado ótimo pode
não se repetir na rodada seguinte. Só substitui se o novo consumo for
estritamente menor (empate não troca).

### 12.1 Rede neural das receitas (`encaixe-rede.js`) — camada nova, já portada

Complementa a memória por "balde exato" (assinatura, §11.12) — que só enxerga
trabalho **idêntico** a um já visto — generalizando para trabalho **parecido**.
~200 linhas de JS puro, sem biblioteca (o instalador já embute um Node
standalone; trazer TensorFlow custaria dezenas de MB para um problema pequeno).

**Arquitetura**: rede densa feed-forward simples, camadas `[34, 16, 8, 1]`
(34 = 12 números do trabalho + 22 do one-hot da receita). Ocultas com `tanh`,
saída com `sigmoide` (a previsão é uma chance, 0 a 1, da receita ganhar).
Pesos inicializados por escala de Xavier (`sqrt(2/(entrada+saida))`), viés em
zero. Treino por retropropagação manual, gradiente descendente simples (sem
otimizador), `entropia cruzada` (o gradiente da última camada simplifica pra
`saída - alvo`), 150 épocas, taxa 0.05, embaralhando a ordem dos exemplos a
cada época.

**Vetor do trabalho (12 números, `vetorDoTrabalho`)** — a mesma matéria-prima
de `assinaturaDoTrabalho` (§11.12), mas sem arredondar pra caber num texto de
balde:
```
[0]  log2(1+nPecas)/6
[1]  min(2, larguraTecido/300)
[2..5]  média, desvio, mín, máx da OCUPAÇÃO das peças (ocupacao ?? 1)
[6..9]  média/3, desvio/3, clamp(mín/3,-1,1), clamp(máx/3,-1,1) de log2(largura/altura)
[10] fração de peças com giro "livre"
[11] fração de peças com giro "fixa"
```

**Vetor da receita (22 números, one-hot, `vetorDaReceita`)**:
```
motor (4): contorno | retangulo | faixas | nfp
agrupamento (7): solta | dupla | trio | quarteto | cruzada | deitada | empe
ordem (4): area | altura | lado | largura
heuristica (7): fundo | vazio | bl | bssf | blsf | baf | encosta
```
(`corte`, só do motor faixas, fica fora do vocabulário — é contínuo por
trabalho, não categórico, e faixas já perde na maioria dos casos medidos.)

**Treino** (`talvezRetreinar`, servidor, dentro do POST `/memoria`):
- Um exemplo por `(trabalho, receita tentada)` — não só a vencedora; sem as
  perdedoras a rede não aprenderia a diferença. `alvo = 1` se a receita venceu
  aquela busca, `0` senão.
- Reconstruído do zero a cada retreino, varrendo `encaixe_historico` inteiro
  (linhas com `features`/`placar` não nulos).

> **Correção deliberada no port (01/09/2026, `docs/guia-melhorias-aproveitamento-encaixe.txt`
> §6.1) — divergência intencional do JS original.** O `alvo` do JS usa
> `placar[receita].vitorias > 0`, e essa contagem (§12.1 acima, `LinhaDoPlacar`)
> conta quantas vezes a receita virou o **recorde momentâneo dentro da própria
> fatia** durante a busca — não se ela foi a vencedora do encaixe inteiro. Uma
> receita pode "brilhar" cedo numa fatia e perder no fim para outra; o JS (e a
> primeira versão deste port) rotulava isso como vitória de treino, ensinando a
> rede a reconhecer "foi boa num instante", não "foi a melhor escolha" — viés
> que infla falsos positivos. O port corrigiu: `alvo = 1` só quando
> `linha.Receita == historico.Receita` (a vencedora REAL do encaixe, guardada
> na própria linha do histórico), `0` nas demais — implementado em
> `EncaixeMemoriaService.TalvezRetreinarAsync`, coberto por um teste de
> regressão que treina com um cenário armado exatamente pra provar a diferença
> (`TalvezRetreinar_RotulaPeloVencedorFinalDoEncaixe_...`, em
> `EncaixeMemoriaServiceTests`) — sem a correção, o teste falha revertendo a
> rede a apostar no perdedor. Este é o primeiro item do roadmap do guia (§17,
> etapa 1) já aplicado; os demais (features de quantidade/família, workers com
> papéis fixos, top-K de candidatos, busca local, NFP seguro, benchmark
> estatístico) seguem como próximos incrementos, não ainda portados.
>
> **Atualização (02/09/2026) — o projeto de referência foi além, alvo virou
> contínuo.** `C:\projetos\optmize-full` (o repositório original, atualizado
> de verdade, com commits reais) evoluiu essa mesma correção: em vez de
> `alvo = 1` só pra campeã e `0` pras demais (o que este port já fazia,
> corretamente, desde a correção acima), a campeã continua valendo 1, mas as
> outras agora valem O QUANTO CHEGARAM PERTO dela — alvo contínuo, caindo
> linearmente até zerar aos 5% atrás do consumo da campeã (`ALVO_TOLERANCIA`).
> Motivo medido lá: tratar "ficou 0,3% atrás" igual a "ficou 8% atrás" (os
> dois viravam `0`) descartava informação real — a rede aprendia só
> "venceu/não venceu" quando a pergunta certa é "quão perto". Um segundo
> defeito veio junto: `melhorConsumo` da receita era gravado mesmo em
> tentativa que deixou peça de fora (encaixe incompleto "parece" mais barato
> por ter menos peça) — corrigido junto com o item de "não-encaixados" logo
> abaixo (§11.7).
>
> Portado aqui: `LinhaDoPlacar`/`LinhaDoPlacarDto` ganharam
> `MelhorConsumoCm` (o placar da receita já guardava isso internamente,
> só não saía do `PlacarDeReceita` pro histórico persistido);
> `EncaixeMemoriaService.AlvoDaReceita` replica a fórmula exata (campeã=1,
> `Math.Max(0, 1 - atras/0.05)` pras demais, `atras = (meu-campeão)/campeão`).
> Histórico gravado ANTES desta versão não tem `MelhorConsumoCm` — o alvo
> cai pra `0` nesse caso (só sabe "não foi campeã"), sem quebrar nem exigir
> migração de banco, igual ao original.

### 11.7-bis Menos peça de fora sempre vence — bug real do port, achado no projeto de referência

> **02/09/2026.** Comparando com `optmize-full` atualizado, achamos que a
> comparação "é melhor que" do JS (`melhorQue`, no motor de busca) SEMPRE
> prioriza menos itens não-encaixados, e só usa `consumo` pra desempatar
> quando os dois lados têm a MESMA quantidade de peça de fora. Nosso port
> nunca tinha essa checagem — `RodarTentativa`/`BuscaParalela` comparavam
> só por `ConsumoCm`, então uma tentativa que deixou peça de fora (menos
> peça, menos tecido gasto, número mais baixo — mas encaixe incompleto)
> podia sair "vencedora" só pelo número, mesmo perdendo pra uma tentativa
> completa de consumo maior. Em produção isso nunca apareceu nos nossos
> testes (todo lote real desta sessão sempre encaixou 100% das peças em
> pelo menos uma receita), mas é exatamente a classe de bug silencioso —
> só aparece em lote difícil — que este guia pede pra caçar de propósito.
>
> Corrigido com `PlacarDeReceita.EhMelhor(naoEncaixados, consumo, ...)`
> (porte direto de `melhorQue`), usado tanto dentro de uma fatia
> (`BuscaDeReceitas.RodarTentativa`, com o MESMO cuidado de capturar o
> "antes" já documentado acima pra não reintroduzir o bug de auto-comparação)
> quanto na combinação entre fatias (`BuscaParalela`, agora
> `OrderBy(NaoEncaixados).ThenBy(ConsumoCm)` em vez de só `ConsumoCm`).
> `ResultadoDaBusca` ganhou `MelhorNaoEncaixados`. Testes de regressão
> dedicados em `PlacarDeReceitaTests` e `BuscaDeReceitasTests` (uma receita
> "econômica" que deixa peça de fora nunca vence uma completa, mesmo com
> consumo bem menor) — confirmado que falham no código antigo e passam com
> a correção. 412 testes passando, sem regressão no lote real de 25
> arquivos/57 peças (6,75-6,94m, mesmo patamar de antes).

### 11.7-ter "Reparo guiado" — busca local direcionada, portada do projeto de referência

> **02/09/2026.** Também achado comparando com `optmize-full` atualizado:
> em vez de sacudir a ordem inteira sem direção ao "Refinar" (`Embaralhamento.Leve`),
> o JS guarda qual UNIDADE (peça ou bloco) sobrou mais buraco morto acima
> dela na melhor tentativa (`vazio`, já calculado pra toda posição mesmo
> quando a heurística vencedora é "fundo" — quase de graça) e, com uma
> chance fixa (`REPARO_CHANCE=0.3`), recoloca só ESSA unidade mais cedo na
> fila em vez de mexer em tudo — dá a ela a chance de escolher uma posição
> melhor, antes que o relevo já esteja mais ocupado.
>
> Portado como `Embaralhamento.RepararPior` (Core, 5 testes: permutação
> válida, o bloco se move pra uma posição igual-ou-mais-cedo preservando a
> ordem relativa entre seus itens, casos de borda de bloco já no início/
> itens inexistentes) + rastreio da "pior unidade" em
> `EncaixeService.ExecutarContornoComPerfil` (`ResultadoMotor.PiorUnidadeItens`,
> extraído de `PosicaoEncontrada.P1`/`P2` conforme a heurística — Fundo usa
> P2, Vazio usa P1, Contato não tem essa noção e nunca vira "pior") +
> `PlacarDeReceita.MelhorPiorUnidadeItens` (acompanha `MelhorOrdem`,
> atualizado junto) + `BuscaDeReceitas` (usa `RepararPior` em vez de `Leve`
> com 30% de chance, só quando "Refinar" E existe uma pior-unidade
> conhecida — retângulo/NFP nunca preenchem isso, então caem sempre em
> `Leve`).
>
> **Medido** (mesmo lote real do concorrente, 179cm/4mm/margem 0, 60s,
> comparação A/B isolada — 3 rodadas cada, só trocando `ReparoGuiadoChance`
> entre 0.3 e 0): **com reparo, 2,492-2,516m (média ≈2,507m); sem reparo,
> 2,510-2,518m (média ≈2,515m)** — toda rodada COM reparo bateu ou empatou
> a MELHOR rodada sem reparo. Ganho pequeno (~0,1-0,3%) mas consistente nas
> 3 repetições, ao contrário do `ReconstruirRabo` (§11.7, testado antes e
> revertido por empatar dentro do ruído) — a diferença é que este mexe de
> forma INFORMADA (sabe qual peça errou), não às cegas. Mantido ligado.
> 417 testes passando, sem regressão no lote de 25 arquivos/57 peças.

- Só treina de novo com ≥30 exemplos acumulados (`REDE_MINIMO_PARA_TREINAR`),
  e só se ≥20 exemplos novos surgiram desde o último treino
  (`REDE_RETREINO_A_CADA`) — o ganho de retreinar a cada encaixe salvo não
  compensa o custo.
- Falha de treino nunca derruba o salvamento do encaixe (mesma regra de
  "acelerador, não requisito" do resto da memória) — só loga aviso.

**Uso na busca** (`encaixe-motor.js`):
1. Servidor manda no GET `/memoria`: `rede` (pesos), `redeExemplos`,
   `redeFormatosDistintos` (assinaturas distintas vistas), `redeMadura` —
   `true` só quando `exemplos >= 200` **e** `formatosDistintos >= 20`
   (`REDE_LIMIAR_MADUREZA`/`REDE_LIMIAR_DIVERSIDADE`). Volume sozinho engana:
   uma loja que repete os mesmos 6 formatos pode acumular milhares de exemplos
   sem a rede nunca ter visto formato diferente — medido: com 6 formatos
   distintos, a rede acertou só 4 de 6 ao apontar a receita certa num formato
   novo, e nos 2 erros pontuou a vencedora de verdade perto de 0%.
2. Antes de fatiar entre workers, pontua cada receita candidata:
   `pontuarReceitas(rede, vetorTrabalho, chaves)`.
3. **Se `redeMadura`**: corta (`filtrarPorRede`) as receitas com pontuação
   `< 0.05` (`REDE_CORTE_LIMIAR`) — **mas nunca um motor inteiro**: se nenhuma
   receita de um motor passou do corte, mantém todas as dele (sinal de que a
   rede não tem opinião boa pra aquele motor neste trabalho, não que o motor
   é ruim).
4. Peso de cada receita pra ordenar a **passada base** (só a base — no sorteio
   das tentativas seguintes todas valem igual, insistir nas vencedoras já
   medido como pior):
   `historico = max(vitorias/usos do balde exato, pontuação da rede)` — o
   maior dos dois, nunca a média: o balde só enxerga idêntico, a rede só
   enxerga parecido, um cobre o buraco do outro sem piorar o que o outro já
   sabia. `peso = 1 + historico*4`.

**Onde isso fica no port .NET** (portado 01/09/2026):
- **Core** (`OptimizePro.Core.Encaixe.Busca`, matemática pura, sem I/O):
  `RedeDeReceitas` (arquitetura/treino/previsão/pontuação em lote),
  `VocabularioDeReceita` (chave/one-hot de 22 números por receita),
  `VetorizacaoDoTrabalho` (12 números do trabalho) e `AssinaturaDeTrabalho`
  (o balde exato de §11.12, mesma matéria-prima arredondada). `RedeNeural`/
  `CamadaDaRede` são POCOs mutáveis (não records) de propósito, pra
  `System.Text.Json` serializar/deserializar sem conversor customizado — o
  mesmo formato do `pesos` salvo em `encaixe_rede_pesos` (§3.10.1), então dá
  pra importar pesos já treinados de um `dados.db` real se algum dia houver
  um. `BuscaDeReceitas.BuscarMelhorEncaixe`/`BuscaParalela` ganharam um
  parâmetro opcional `pesoDaReceita` — o antigo stub `PesoDaMemoria` (que
  sempre devolvia `0`) foi removido; quem decide o peso agora é a camada de
  Services, que tem acesso à memória persistida.
- **Data** (`OptimizePro.Data`): colunas `features`/`placar` (JSON) em
  `encaixe_historico`, tabela `encaixe_rede_pesos` (linha única, id=1) — ver
  §3.10/§3.10.1. `IEncaixeMemoriaRepository` ganhou métodos pra ler/gravar os
  pesos e listar o histórico elegível pro treino.
- **Services** (`OptimizePro.Services.Encaixe`): `EncaixeMemoriaService`
  implementa `talvezRetreinar` (`TalvezRetreinarAsync`, mesmos limiares —
  30/20/200/20) e expõe `RedeMadura`/`RedeExemplos`/`RedeFormatosDistintos`/
  `Rede` em `ConsultarMemoriaAsync`. `EncaixeService.BuscarMelhorEncaixeAsync`
  calcula assinatura+vetor do trabalho, consulta a memória, pontua as receitas
  candidatas com a rede (se existir — poda só se madura, `FiltrarPorRede`),
  pesa a passada base (`historico = max(balde, rede)`, `peso = 1+historico*4`)
  e, ao final, registra a busca inteira (placar completo, não só a vencedora)
  via `RegistrarResultadoDaBuscaAsync` — nunca deixa uma falha de memória/treino
  derrubar o resultado do encaixe em si (mesma regra "acelerador, não
  requisito" do sistema original).

---

## 13. Geração de PDF do encaixe

Biblioteca atual: **pdfkit**. Fluxo em duas etapas:

1. **Upload prévio das artes**: cada arte de peça é enviada em binário puro
   (`POST /api/encaixe/arte?sessao=&chave=`), guardada **em memória do
   processo** (`Map<sessao, {criadaEm, artes: Map<chave, Buffer>}>`), TTL de 10
   minutos.
2. **Montagem do PDF** (`POST /api/encaixe/pdf`): body JSON com dimensões e
   posições; imagens já pré-desenhadas e **já rotacionadas** no cliente (não há
   rotação dentro do PDF).

**Montagem da página**:
```
PT_POR_CM = 72/2.54
larguraPt = larguraTecido*PT_POR_CM; alturaPt = consumo*PT_POR_CM
LIMITE_PT = 14400  // 200 polegadas ≈ 508cm, máximo aceito por PDF padrão
```
Página **única**, sem margem/cabeçalho/rodapé — só o desenho puro, porque o
arquivo vai direto para o RIP de uma plotter que espera o rolo inteiro numa
página contínua.

Cada imagem é aberta **uma única vez** (`openImage`) e cacheada por `chave` —
evita reembutir a mesma arte repetidamente quando a peça se repete dezenas de
vezes no encaixe (otimização crítica de tamanho/performance).

**Lógica do `/UserUnit`** (páginas maiores que o limite do PDF):
```
maiorLado = max(larguraPt, alturaPt)
se maiorLado <= 14400: unidade = 1
senão: unidade = ceil((maiorLado/14400)*100)/100   // arredondado p/ cima, 2 casas
página criada com tamanho [larguraPt/unidade, alturaPt/unidade]
se unidade != 1: seta manualmente doc.page.dictionary.data.UserUnit = unidade
todas as coordenadas de desenho são divididas por `unidade` antes de passar ao pdfkit
```
`/UserUnit` diz ao leitor de PDF para multiplicar cada unidade de página pelo
fator — restaura o tamanho real de impressão em escala 1:1 mesmo com a página
"de arquivo" menor que o real. **Verificar se a lib .NET escolhida
(QuestPDF/PDFsharp/iText7) expõe isso nativamente**; se não, será necessário
escrever o dicionário PDF diretamente como faz o código atual (poucas libs
expõem UserUnit).

Response: stream direto (nunca salvo em disco no servidor); sessão removida do
Map em memória ao final.

---

## 14. Módulo Vetor (raster → SVG)

Arquivos: `public/vetor.js` (1196 linhas), `vetor-worker.js`, `vetor-tela.js`.

### 14.1 Pipeline (função `vetorizarImagem(dados, opcoes)`)

1. **`juntarCores`** — quantização por median cut → paleta + índices por pixel.
2. **`limparCisco`** — remove manchas pequenas via flood-fill, reatribui à cor
   vizinha dominante.
3. Por cor da paleta: **`contornosDoMapa`** — extrai contornos fechados
   (externos e furos) via percurso de arestas de célula.
4. **`afinarNoSubpixel`** (opcional) — desloca cada ponto para a posição
   subpixel real via anti-aliasing.
5. **`acharFormaRedonda`** — testa círculo/elipse por mínimos quadrados; se
   passar, emite arcos SVG e pula os passos seguintes.
6. **`simplificar`** (Douglas-Peucker, tolerância proporcional ao "lado menor").
7. **`acharQuinas`** + **`tangentesDoContorno`**.
8. **`caminhoDoContorno`** — remonta em reta (`L`)/arco (`A`)/Bézier (`C`).
9. Monta `<path>` por camada, concatena em `<svg>`.

Imagem sempre reamostrada para máx. **1800px** de lado maior antes de
vetorizar (qualidade, não memória — evita que compressão JPEG vire milhares de
contornos invisíveis).

**Parâmetros** (todos vindos da UI):

| campo | default | range | efeito |
|---|---|---|---|
| cores | 6 | 1-32 | nº de cores da paleta (1=silhueta) |
| detalhe | 8 | ≥0 | tamanho mín. (px) de mancha para não ser cisco |
| suavidade | 1 | ≥0 | tolerância de simplificação |
| quina | 55 | graus | abaixo disso = canto vivo |
| tensao | 1 | 0-2 | comprimento das alças de Bézier |
| redondas | true | bool | detecção de círculo/elipse |
| porTrechos | true | bool | remontagem reta/arco/curva (off = tudo Bézier) |
| subpixel | true | bool | afinamento por anti-aliasing |
| juntarSombras | 0 | 0-100 | funde luz/sombra via cromaticidade |

4 atalhos pré-configurados: `chapada`, `silhueta`, `sombra`, `fino`.

### 14.2 Quantização de cores (median cut + relaxamento)

**Espaço de agrupamento** (`posicaoDaCor`):
```
se juntarSombras<=0: (r,g,b) puro
senão:
  peso = 1 - 0.85*min(1,juntarSombras/100)
  soma=r+g+b+1
  p0=(r/soma)*441; p1=(g/soma)*441; p2=((soma-1)/3)*peso   // cromaticidade + luminância com peso decrescente
```

**Histograma**: quantiza a RGB555 antes de agrupar; conta preferencialmente
pixels de "miolo" (longe de bordas, via `marcarBordas`); se miolo <5% do total,
recontagem inclui tudo.

**Median cut**: corta pelo **peso em pixels** (não por quantidade de cores
distintas) — soma `n` até passar da metade do peso da caixa.

**Relaxamento tipo Lloyd** (até 8 passadas): reatribui cada cor do histograma
à cor de paleta mais próxima, recalcula médias ponderadas, para quando a maior
movimentação < 0.5.

### 14.3 Extração de contorno (marching-squares nas quinas)

Anda pelas **quinas das células** (não centros), orientação consistente
("cheio à direita do sentido de percurso"). Múltiplas arestas saindo do mesmo
vértice (sela) resolvidas por menor "giro" (produto vetorial/escalar entre
entrada e candidatas).

### 14.4 Subpixel (anti-aliasing)

Para cada ponto do contorno: calcula normal local, sonda a cor real (bilinear)
em ±0.5px ao longo da normal, projeta na reta entre cor-de-dentro e
cor-de-fora, interpola o deslocamento subpixel (`-0.5..0.5`). Cache
compartilhado entre camadas garante que a borda entre duas cores fique
coincidente.

Ganho medido: 27× menos nós com mais fidelidade, num caso de teste.

### 14.5 Detecção de círculo/elipse (mínimos quadrados de Kåsa)

```
Sxx=Σu², Sxy=Σuv, Syy=Σv², Sxz=Σuz, Syz=Σvz   (u=x-mx, v=y-my, z=u²+v²)
det = Sxx*Syy - Sxy²
cx = mx + (Sxz*Syy - Syz*Sxy)/(2*det)
cy = my + (Syz*Sxx - Sxz*Sxy)/(2*det)
r = média das distâncias ao centro
```
Duas peneiras evitam falso positivo: erro máximo dentro da tolerância, **e**
área do contorno bate com `π*r²` (±6%).

### 14.6 Remontagem reta/arco/curva

Para cada trecho: testa até onde vai uma reta (distância perpendicular dos
pontos brutos à corda), até onde vai um arco (ajuste de Kåsa no trecho), arco
só vence se alcançar significativamente mais longe que a reta. Senão, gera
Bézier cúbica usando as tangentes calculadas (não apontando para o vizinho
seguinte — isso é o que faz a curva não ondular).

**Construção do comando de arco SVG** (delicado — replica a fórmula do próprio
elemento `<path>` `A` do SVG para achar o centro a partir de raio+2 pontos+2
flags, testando as 4 combinações e escolhendo a que bate contra os pontos
brutos reais — não contra o polígono simplificado).

### 14.7 "Juntar sombras"

Controla `posicaoDaCor` (§14.2) para tratar luz/sombra da mesma cor como uma
cor só, comparando **proporção entre canais** em vez de RGB puro. Desligado
por padrão (0) — medido que piora arte comum e só ajuda em degradê/sombreado.

### 14.8 Performance

Roda em Web Worker (`vetor-worker.js`), pixels transferidos sem cópia — em
.NET, equivale a rodar em `Task.Run` separado da UI thread (não precisa de
mensageria, pode chamar direto assíncrono).

**Recomendação para C#**: implementar `Vector2`/geometria própria; usar
SkiaSharp/ImageSharp só para decodificar/reamostrar a imagem (atenção ao
algoritmo de interpolação — influencia o resultado); todo o resto (quantização,
flood-fill, contorno, Douglas-Peucker, ajuste de círculo/arco, Bézier) é lógica
pura portável quase 1:1, função por função.

---

## 15. Disparo de mensagens WhatsApp (descontinuado neste port — ver §15.1)

Já coberto tecnicamente em §5. Ponto de atenção arquitetural:

**`whatsapp-web.js` não tem equivalente direto em .NET.** Ele funciona
controlando o WhatsApp Web via Puppeteer/Chromium headless (simula um
navegador logado). Opções para o porte:

1. **Manter um processo Node lateral só para isso** — o app .NET spawna um
   pequeno processo Node/`whatsapp-web.js` (ou uma lib como Baileys, que
   reimplementa o protocolo do WhatsApp Web sem precisar de navegador) e se
   comunica com ele via IPC/HTTP local.
2. **WhatsApp Business API oficial** — muda completamente o modelo (requer
   conta business aprovada, custo por mensagem, sem QR code) — provavelmente
   fora do espírito do sistema atual (uso pessoal/pequena empresa via QR code).
3. **Bibliotecas .NET de terceiros para protocolo WhatsApp Web** — avaliar
   maturidade/manutenção antes de depender.

Qualquer que seja a escolha, a lógica de negócio a preservar é: normalização de
número (§5.3), template `{{nome}}`, delay aleatório entre envios, log CSV
incremental, e os eventos de status (`qr`, `pronto`, `desconectado` etc.)
mapeados para SignalR.

### 15.1 Direção decidida pro port (01/09/2026) — substitui o plano acima

Decisão com o usuário: o Disparo **não** vai ser o disparo de mensagens em
massa via WhatsApp descrito acima. A ideia nova é **captação de lead**: o
módulo recebe dados de lead e envia pra uma base **separada (Supabase)**, via
notificação/webhook **silencioso** — ou seja, grava em segundo plano, sem
alertar o usuário do Optimize na hora (sem popup/som/badge).

**Em aberto, não decidido ainda:**
- De onde vem o lead (mensagem recebida no WhatsApp? formulário dentro do
  próprio Optimize? outra origem?) — perguntado ao usuário em 01/09/2026, a
  resposta foi "deixar em stand-by" antes de detalhar.
- Layout dos dados gravados no Supabase (schema da tabela/coleção de leads).
- Se ainda existe alguma peça de WhatsApp nesse fluxo (ex.: continuar
  escutando mensagens recebidas via `whatsapp-web.js`/sidecar pra capturar o
  lead) ou se a origem é 100% desacoplada do WhatsApp.

**Status no port .NET:** `Optimize.App/ViewModels/DisparoViewModel.cs` e
`DisparoView.axaml` continuam como placeholder (nunca chegaram a ser
implementados) — módulo em **stand-by** até a origem do lead ser definida.
`Optimize.App/ViewModels/ConfiguracoesViewModel.cs` já tem um aviso na tela
avisando que "conectar/desconectar WhatsApp" (o plano antigo) só aparece
quando esse componente existir — esse texto também precisa ser revisado
quando o desenho novo for fechado, já que pode nem ser sobre WhatsApp.

---

## 16. Empacotamento desktop (Tauri → proposta .NET)

Já coberto em §2.2. Resumo dos pontos técnicos do Tauri atual, para referência:

- Processo filho via `std::process::Command` (não o sidecar formal do Tauri).
- Porta livre descoberta via bind em `127.0.0.1:0`.
- Polling de conexão TCP até o backend responder (timeout 25s).
- `CREATE_NO_WINDOW` para esconder console do processo filho no Windows.
- Janela criada programaticamente (não via config estática), WebView aponta
  para a URL local do backend (não IPC nativo do Tauri).
- Dados do usuário em `%APPDATA%\com.arteof.optimize`.
- Dois builds sequenciais: `pkg` empacota o Node como `.exe` standalone → esse
  `.exe` é copiado para dentro do bundle Tauri como recurso → `tauri build`
  gera o instalador NSIS final.

Em .NET, tudo isso simplifica: publicação self-contained (`dotnet publish -r
win-x64 --self-contained`) gera um único executável; WinUI 3/WPF hospeda o
Kestrel embutido no mesmo processo (sem spawn de processo filho, sem descoberta
de porta livre por bind, sem polling — o servidor sobe e a janela é criada em
sequência direta) e usa `Microsoft.Web.WebView2` para exibir a UI.

---

## 17. Regras transversais e armadilhas já pagas

Do `docs/MAPA.md`, relevantes para não repetir erros durante o porte:

1. **Nome repetido entre arquivos** (JS): causou bug onde uma função sobrescrevia
   outra silenciosamente, medindo peça errada. Em C#, namespaces/classes
   evitam isso estruturalmente — mas vale atenção a **nomes de propriedades em
   DTOs compartilhados** entre módulos.
2. **A medida em centímetros vem sempre do arquivo (DPI), nunca do bitmap
   decodificado** — decodificação reduzida daria peça menor do que é de
   verdade. Preservar essa regra ao portar os leitores de imagem.
3. **Miniatura nunca é a arte inteira reduzida por CSS/atributo de tela** — sempre
   gerar e persistir uma miniatura real reduzida, para não forçar decodificação
   completa da arte de impressão só para desenhar um ícone.
4. **A faxina de arquivos órfãos verifica contra a tabela inteira**, nunca só
   contra o registro que mudou — a mesma imagem pode estar em uso por outro
   registro (§6).
5. **Falha de rede/servidor não pode derrubar o Encaixe** — a memória de
   aprendizado é um acelerador, não um requisito; se indisponível, a busca
   roda igual, só sem aprendizado prévio.
6. **Cor só sai de um único lugar** (no frontend atual, `interface.css`) — ao
   portar para um design system .NET, manter um único ponto de verdade para a
   paleta (resource dictionary / design tokens), nunca cores hardcoded
   espalhadas.
7. **Tabela de banco com nome reaproveitado**: `projeto_clientes` (não
   `clientes`) por causa de instalações antigas — ver §3.7. Aplica-se também a
   qualquer outra tabela cujo nome possa colidir com algo do módulo comercial
   legado.
8. **Contas geométricas duplicadas**: `areaComSinal` existia 2x, idêntica, em
   arquivos diferentes. Ao portar, centralizar toda geometria pura numa única
   biblioteca (`Geometria` estática, §8.2) e nunca duplicar fórmulas.

---

## 18. Plano de porte sugerido

Ordem de implementação recomendada (dependências primeiro), consolidando a
sugestão dos agentes de pesquisa:

1. **Geometria pura** (§8.2) — biblioteca `Geometria` sem dependência de UI;
   ponto de partida mais seguro, testável isoladamente.
2. **Persistência** — modelos EF Core espelhando o schema §3, migrations
   iniciais, serviço de acesso a dados.
3. **Silhueta/máscara/engorde/rotação** (§11.2) — processamento de bitmap
   `bool[,]`, sem UI.
4. **Estruturas de bloco** (`Forma`, `formaDePartes`, `encostarNaForma`,
   `formasDoBloco`, §11.8).
5. **Encaixador de contorno** (`melhorPosicaoDaUnidade`/`encaixarContorno`,
   §11.3) — o núcleo, testável isoladamente contra os números documentados
   aqui e no README (regressão numérica).
6. **Encaixador retângulo/MaxRects** (§11.4).
7. **NFP** (§11.5) — mais complexo; pode ficar por último, já que perde na
   maioria dos casos de teste.
8. **Motor de receitas + busca** (poda/parede/perseguição, §11.7).
9. **Paralelização** via `Task`/`Parallel` (§11.9) — mais simples que o
   original, memória compartilhada direta.
10. **Parsers de molde** (DXF → PLT → PDF → SVG, nessa ordem de dificuldade
    crescente dado que SVG depende de path-flattening manual em .NET, §8.1).
11. **API REST** (ASP.NET Core Minimal API, §4) espelhando rotas/contratos.
12. **SignalR** substituindo Socket.IO (§5) — cliente também precisa ser
    reescrito.
13. **Vetorização** (§14) — pipeline independente, pode ser portado em
    paralelo às demais frentes.
14. **Geração de PDF** (§13) — validar se a lib escolhida suporta
    `/UserUnit`; se não, escrever o dicionário manualmente.
15. **Desenho/exportação** (canvas → SkiaSharp).
16. **Disparo WhatsApp** (§15) — decidir estratégia de integração antes de
    investir tempo aqui (é a peça com maior risco de não ter equivalente
    direto).
17. **Shell desktop** (§16) — WinUI 3/WPF + WebView2, por último, quando as
    peças de backend já estiverem funcionando via `dotnet run` local.
18. **Regressão numérica**: para o encaixe e a vetorização, comparar saídas do
    C# contra a implementação JS atual nos mesmos arquivos de teste
    mencionados no README (camiseta+manga+gola, misturado pequeno, etc.) —
    esses números documentados servem de gabarito de correção do porte.

---

## 19. Licenciamento / acesso controlado (02/09/2026)

Requisito do cliente: liberar o software só pra quem pagou, mas o app **tem
que funcionar 100% offline** — o computador do cliente pode nunca ter
internet, então checagem via HTTP contra um servidor está fora de cogitação
(bastaria desligar a internet pra usar sem bloqueio). A solução adotada é a
mesma categoria de mecanismo de chaves de produto de software comercial:
**assinatura digital assimétrica verificada localmente**, sem qualquer
dependência de rede.

### 19.1 Formato do código de licença

Payload de 8 bytes + assinatura ECDSA P-256 de 64 bytes (r‖s), codificados em
Base32 Crockford (alfabeto sem ambiguidade — sem I/L/O/U) com traços a cada 5
caracteres pra ficar digitável por humano:

```
byte 0:    versão do formato (permite evoluir o payload sem quebrar códigos antigos)
byte 1:    tipo (0 = Paga, 1 = Teste)
bytes 2-3: dias desde 2025-01-01 (big-endian) até quando o código vale
bytes 4-7: hash de 32 bits do identificador do cliente (big-endian) — não
           autentica nada sozinho, é só rastreabilidade (saber pra quem foi
           gerado aquele código)
bytes 8-71: assinatura ECDSA (SHA-256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
            sobre os 8 bytes de payload
```

Note que `DSASignatureFormat.IeeeP1363` **não existe** no .NET — o nome certo
é `IeeeP1363FixedFieldConcatenation`; o formato padrão do `SignData` sem essa
flag é DER (variável, complica o parsing binário fixo acima).

`OptimizePro.Licenciamento/CodificadorDeLicenca.cs` implementa `Gerar` (exige
a chave PRIVADA) e `Verificar` (só a chave PÚBLICA, nunca lança exceção —
qualquer código malformado/adulterado só devolve `null`).

### 19.2 Duas chaves, dois lugares MUITO diferentes

- **Chave privada** — só existe na máquina de quem vende (nunca no app do
  cliente, nunca neste repositório de forma real). Usada pela ferramenta
  `Ferramentas/GeradorDeLicenca` (console app separado) pra gerar códigos após
  confirmar pagamento: `gerar-chave` cria o par; `gerar-codigo --chave <pem>
  --cliente <id> --dias <N> [--tipo pago|teste]` gera um código (dias
  configurável cobre tanto licença mensal quanto teste de 7 dias — é o mesmo
  mecanismo, só muda o número).
- **Chave pública** — embutida em `LicencaService.ChavePublicaBase64`, viaja
  dentro do app do cliente. Só consegue *verificar* assinaturas, nunca
  *forjar* — por isso é seguro publicá-la.

  **A chave pública atualmente hardcoded em `LicencaService.cs` é uma chave
  de DEMONSTRAÇÃO gerada durante o desenvolvimento** (a privada correspondente
  apareceu em texto claro nos testes e nesta conversa). **Antes de vender pra
  qualquer cliente real**, rodar `dotnet run --project
  Ferramentas/GeradorDeLicenca -- gerar-chave`, guardar a nova chave privada
  em local seguro (fora do repositório) e colar a nova chave pública em
  `LicencaService.ChavePublicaBase64`.

### 19.3 Estado local e defesa contra "voltar o relógio"

`LicencaService` persiste o código ativado num arquivo (`licenca.dat`, em
`CaminhosDoApp.ArquivoDeLicenca`) criptografado com Windows DPAPI
(`ProtectedData`, `DataProtectionScope.CurrentUser`) — decifrável só pelo
mesmo usuário Windows na mesma máquina; qualquer falha ao ler (arquivo
ausente, corrompido, de outra máquina/usuário) é tratada como "nunca ativado",
nunca lança exceção.

Como não há servidor pra consultar a hora real, um cliente poderia tentar
"voltar o relógio do Windows" pra reviver um código vencido. Mitigação:
`EstadoPersistido` também guarda `MaiorDataJaVista`, um marcador que só
avança — se a data do sistema aparecer *antes* desse marcador,
`ObterEstado()` devolve `SituacaoDaLicenca.RelogioSuspeito` (bloqueia) em vez
de aceitar a data suspeita.

**Limitação reconhecida e comunicada ao cliente**: nenhuma checagem
inteiramente client-side é inquebrável contra um atacante determinado com
acesso físico/local à máquina (pode-se sempre, em teoria, editar o binário ou
forjar o relógio de outras formas). Este design é um mecanismo prático e
padrão do mercado (mesma categoria de chave de produto comercial), não DRM
absoluto — é proporcional ao risco real do caso de uso.

### 19.4 Fluxo na UI (`App.axaml.cs`)

Antes de criar a `MainWindow`, o app resolve `LicencaService.ObterEstado()`.
Se `Liberado == false`, abre `LicencaWindow` (com `LicencaViewModel`) no lugar
da janela principal — se o estado for `Expirada` ou `RelogioSuspeito`, a tela
já mostra o motivo (`DefinirMotivoDoBloqueio`) em vez de só pedir "digite o
código". Só quando a ativação dá certo (`Ativado == true`) a `LicencaWindow`
fecha e o app troca `desktop.MainWindow` pela `MainWindow` de verdade —
`desktop.ShutdownMode` é `OnLastWindowClose` justamente pra essa troca não
derrubar o app no meio do caminho. Se o usuário fechar a `LicencaWindow` sem
ativar, o app encerra.

---

## 20. Módulo Vetor — traçado próprio vs. "Plano B" com Potrace externo (02/09/2026)

Depois de várias correções reais no traçado próprio (ordem de pintura por área, cor de
fora do subpixel amostrada localmente, ajuste de reta/arco checado contra o contorno
BRUTO em vez de só os pontos já simplificados, tolerância relativa ao tamanho de cada
contorno, histograma de cor ignorando pixel de borda/anti-aliasing — cada uma medida e
documentada em commits/testes de regressão dedicados), a qualidade em arte complexa
(logo com degradê + tipografia fina) ainda ficou abaixo do necessário — pedido explícito
do usuário: "vamos para o plano b".

### 20.1 Por que um processo separado, e não uma biblioteca

O Potrace é o algoritmo padrão-ouro de bitmap→vetor, muito mais maduro que o traçado
próprio. Existe um porte em C# real, `BitmapToVector`/`BitmapToVector.SkiaSharp`
(NuGet), mas é **GPL-3.0-or-later**. GPL trata como "obra combinada" (que precisa
inteira sob GPL) qualquer coisa LINKADA — chamada de função direta, mesmo numa DLL
separada, rodando no mesmo processo; simplesmente criar outro `.csproj` na mesma
solução **não** resolve isso.

O padrão que de fato separa (usado por incontáveis apps comerciais que chamam
ferramentas GPL como `ffmpeg` sem virar GPL elas mesmas): rodar como **processo externo
de verdade**, comunicação só por arquivo/stdout, nunca por chamada de função
in-process — "mera agregação" em vez de obra derivada. **Isto não é aconselhamento
jurídico definitivo** — confirmar com um advogado antes de distribuir pra clientes reais.

### 20.2 `Ferramentas/VetorGpl` — o executável GPL, isolado

Console app separado (`Ferramentas/VetorGpl/VetorGpl.csproj`), referencia só
`BitmapToVector.SkiaSharp` — **nunca** deve virar `ProjectReference`/`PackageReference`
de `Optimize.App` ou de qualquer projeto fechado da solução (aviso no topo do próprio
`.csproj`). Contém `LICENSE-GPL-3.0.txt` e um `README.md` explicando a fronteira legal.

Uso: `VetorGpl trace --entrada mascara.png [--turdsize N] [--alphamax X]
[--opttolerance X]` — a entrada é um PNG preto-e-branco (uma camada de cor já
quantizada/limpa pelo chamador); a saída é um array JSON de strings no stdout, cada uma
o atributo `d` de um `<path>` SVG (via `SKPath.ToSvgPathData()`, depois de
`PotraceSkiaSharp.Trace`).

### 20.3 `PotraceProcessoService` (`OptimizePro.Services.Vetor`)

Reaproveita a NOSSA quantização/limpeza (já com a correção de miolo — não há motivo pra
trocar isso, o gargalo estava no traçado de curva, não na cor) e delega só o
traçado-de-contorno-por-camada ao subprocesso: pra cada cor (ordem de área
decrescente), salva uma máscara preto-e-branco temporária, roda `VetorGpl.exe` via
`Process.Start` (stdout redirecionado, JSON parseado com `System.Text.Json`), e monta o
SVG final com `ConversorSvg.MontarSvg` — mesmo formato de saída do motor próprio,
plugável sem mudar `IVetorService`.

`OpcoesDeVetorizacao.UsarMotorExterno` (bool, padrão `false`) escolhe o motor;
`VetorService.VetorizarAsync` decide o branch. Checkbox exposta na UI ("Usar motor
externo (Potrace) — melhor pra arte complexa/degradê"). Se o executável não for
encontrado (`AppContext.BaseDirectory/VetorGpl/VetorGpl.exe` por padrão, injetável pra
teste), lança `InvalidOperationException` com mensagem clara — nunca cai
silenciosamente pro motor próprio (trocar de motor sem avisar seria surpreendente).

### 20.4 Empacotamento (manual por enquanto)

`dotnet publish Ferramentas/VetorGpl/VetorGpl.csproj -c Release -r win-x64
--self-contained false -o <pasta-de-saida-do-app>/VetorGpl` — precisa rodar toda vez que
o `Optimize.App` for publicado/distribuído. Ainda não automatizado (ex.: MSBuild target
`AfterBuild`/`AfterPublish` chamando `dotnet publish` do VetorGpl) — próximo passo se o
plano B se confirmar como caminho definitivo.

**Testado (02/09/2026)**: rodando `VetorGpl.exe` isolado contra uma máscara de letra "Y"
sintética, o Potrace preservou o recorte côncavo corretamente (coisa que o motor próprio
ainda errava nesse mesmo caso) — e o pipeline completo (`PotraceProcessoService` via
`VetorService.VetorizarAsync` com `UsarMotorExterno: true`) rodou ponta a ponta contra
um logo sintético (blob + texto), produzindo SVG com cores e ordem de pintura corretas.

---

## 21. Sincronização com `optmize-full` (08/09/2026) — duas correções portadas do motor de encaixe

Comparação de rotina com o histórico de commits do projeto de referência desde a última
sincronização. A maior parte dos commits recentes de lá é fora do nosso stack — uma macro
de CorelDRAW em VSTA/C# pra personalizar camisa de time (`corel/*.cs`, roda DENTRO do
Corel, não é parte deste app) e uma reforma completa do front-end pra React (`src/`,
irrelevante — o OptimizePro já tem sua própria UI Avalonia). Duas correções de motor,
porém, eram genuínas e portáveis:

### 21.1 Engorde da folga: disco, não mais diamante de Manhattan

> `OptimizePro.Core/Encaixe/Dilatacao.cs` — a referência já tinha corrigido um erro pior
> (engorde horizontal+vertical separável desenhando um QUADRADO, até 41% maior que o raio
> pedido nos cantos em diagonal — ver §11's nota sobre folga real medida em 15,0mm vs
> 10mm pedidos). O port .NET nunca teve esse bug — usava dilatação por diamante de
> Manhattan (`|dx|+|dy|≤raio`) desde o início —, mas o diamante tem o erro OPOSTO: como a
> distância euclidiana é sempre ≤ a distância Manhattan, o diamante é sempre um
> SUBCONJUNTO do disco de mesmo raio — ele SUBDIMENSIONA a margem em contato diagonal.
> Pra raio≤2 as duas formas coincidem exatamente (nenhum teste antigo pegava a diferença);
> a partir de raio≥3 um ponto como (dx,dy)=(2,2) — dentro do disco (distância √8≈2,83),
> fora do diamante (soma 4) — passa a faltar. Errar a margem pra menos é o lado ruim do
> erro (a peça vizinha pode encostar); errar pra mais só gasta um tiquinho de tecido.
> `Dilatacao.Dilatar` (renomeado de `DilatarManhattan`) agora testa `dx²+dy²≤raio²`.
> Teste de regressão dedicado (`Dilatar_Raio3_IncluiCantoDiagonalQueDiamanteAntigoExcluiria`)
> confirma o ponto (2,2) incluído e (3,3) excluído num raio 3. 458 testes passando.

### 21.2 Vetor de features da rede: ponderado por quantidade, não por linha da tabela

> `OptimizePro.Core/Encaixe/Busca/RedeDeReceitas.cs`, `VetorizacaoDoTrabalho.VetorDoTrabalho`
> — achado comparando com `estatisticasPesadas`/`vetorDoTrabalho` em
> `public/encaixe-rede.js` (commit `ca4c171` do projeto de referência). O port calculava
> média/desvio de ocupação e proporção contando cada LINHA da tabela de peças (um formato
> distinto) com peso igual, ignorando `Quantidade` — um trabalho com 1 peça rara e 199
> cópias de outra saía com a média dividida meio a meio entre as duas, quando na prática é
> quase só a segunda que domina o tecido de verdade (mesmo viés afetava a fração de
> giro livre/fixo, e o divisor do "quantas peças" — trocado de /6 pra /8, igual à
> referência, e a contagem em si trocada de "linhas" pra "total de cópias"). `PecaParaRede`
> ganhou `Quantidade` (default 1, então testes/chamadores antigos continuam válidos sem
> mudança); `EncaixeService.cs` já tinha o dado (`PecaParaEncaixar.Quantidade`) e só
> precisou passar adiante. Dois testes de regressão dedicados confirmam a ponderação
> (`VetorDoTrabalho_MediaDeOcupacaoEhPonderadaPelaQuantidade_NaoPorLinhaDaTabela`,
> `VetorDoTrabalho_FracaoDeGiroLivreEFixa_PonderaPelaQuantidade`). **Não portado**: o
> versionamento de features (`REDE_VERSAO_FEATURES`) que a referência usa pra invalidar
> pesos já treinados quando a fórmula do vetor muda — sem clientes reais ainda usando a
> rede treinada, não há peso antigo pra invalidar; fica pra quando isso passar a importar.

### 21.3 Avaliado e adiado — não portado nesta rodada

> - **Ligar o motor "faixas" na lista padrão** (`Receita.DeFaixas` já existe em
>   `OptimizePro.Core/Encaixe/Busca/Receita.cs`): NÃO é wiring barato como pareceu à
>   primeira vista — `EncaixeService`'s dispatcher (`ExecutarContorno`/`ExecutarRetangulo`/
>   `ExecutarNfp`, por volta da linha 279) não tem nenhum caso pra `MotorDeEncaixe.Faixas`;
>   adicionar a receita à lista sem isso quebraria em runtime (switch sem match). Precisa
>   de um `ExecutarFaixas` de verdade, adaptando os dados de peça/grade que `Contorno` já
>   usa. Ganho medido na referência é pequeno e vem de reordenação compartilhada, não do
>   motor "faixas" vencendo sozinho (0 vitórias nos testes deles) — baixa prioridade.
> - **Conversão de cor por perfil ICC** (`cor-api.js`/`cor-icc.js`/`public/cor.js`, ~1100
>   linhas): módulo novo e autocontido (decodifica JPEG CMYK cru, lê o LUT `A2B` do ICC
>   embutido, interpola CLUT, Lab→XYZ com adaptação de Bradford até sRGB) — sem
>   equivalente nenhum no OptimizePro hoje. Genuinamente portável, mas grande o bastante
>   pra merecer sessão própria — fica anotado como candidato futuro, não escopado aqui.

### 21.4 "Encaixe por vãos" — implementado, testado, e REVERTIDO da lista padrão por falta de ganho medido (08/09/2026)

> Porte de `encaixarPorVaos`/`melhorVagaPorVaos`/`descerNosVaos`
> (`public/encaixe-motor.js`, commit `bb1c9cc` da referência) — o motor por lista de
> INTERVALOS ocupados por coluna (em vez de um relevo único), que enxerga o vão que fica
> ACIMA de uma peça já assentada, algo que o relevo simples (`EncaixadorPorContorno`)
> perde pra sempre assim que a peça é assentada.
>
> **Implementado**: `OptimizePro.Core/Encaixe/EncaixadorPorVaos.cs`
> (`TecidoPorVaos`/`MelhorVaga`/`Ocupar`/`DescerNosVaos`, com o mesmo atalho "relevo
> primeiro, descida cara só onde há vão de verdade" da referência — descrito lá como 84%
> do custo do motor) e despachado em `EncaixeService.ExecutarVaos`
> (`MotorDeEncaixe.Vaos`, `Receita.DeVaos`). **Escopo desta primeira versão: só peça
> avulsa** — sem a máquina de blocos dupla/trio/cruzada que `ExecutarContorno` tem (fica
> pro próximo incremento se medição real pedir depois de resolver o ponto abaixo). 5
> testes dedicados em `EncaixadorPorVaosTests`, incluindo um que prova o valor central do
> motor: uma peça pequena descendo e parando DENTRO de um vão fechado por um segmento
> flutuante de outra peça, achando fundo=1 onde o relevo simples exigiria fundo=10.
>
> **Medido no lote real (25-08, 179cm/60s, 2 rodadas) com Vãos na disputa comum**: 251,8cm
> e 252,0cm — ambos dentro da faixa já documentada SEM vãos (251,6-252,8cm, §11.7).
> Contorno venceu as duas rodadas; Vãos nunca venceu. Tentativas caíram de ~590k pra
> ~400-440k (a receita de vãos é mais cara por descer pelos intervalos, e sem fatia
> dedicada ela dilui o orçamento das outras receitas na disputa comum) sem compensar em
> consumo — mesma política de sempre ("sem ganho medido, não vira padrão", igual a Contato
> e ReconstruirRabo). **Revertido de `GeradorDeReceitas.GerarPadrao`** — motor e despacho
> continuam prontos/testados, só não entram na lista padrão.
>
> Isso bate com o que a própria referência mediu: eles só viram ganho real (-1,3~1,5%)
> DEPOIS de dar ao motor uma fatia PRÓPRIA (1/8 do orçamento, não misturada no pool comum)
> — `ParticionamentoDeFatias` já tem o mecanismo de reservar fatia dedicada (usado hoje só
> pro NFP, e mesmo esse está desligado — `reservarUltimaFatiaParaNfp: false` no único
> call site, `EncaixeService.BuscarMelhorEncaixeAsync`). Estender essa reserva pro motor de
> vãos e remedir NESSA configuração é o próximo passo concreto, se algum dia valer a pena
> revisitar — não escopado aqui.
