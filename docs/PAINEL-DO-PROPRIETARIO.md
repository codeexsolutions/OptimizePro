# Painel do proprietário (§23)

> Documento separado de propósito (pedido do usuário, 10/09/2026): este é um contexto à parte
> do resto do porte .NET (`docs/ESPECIFICACAO-PARA-DOTNET.md`, que cobre o chão de fábrica —
> Moldes/Encaixe/Vetor/Disparo e a frota de impressoras). Atualizações aqui não devem exigir
> reler o documento grande, e vice-versa.

## O que é

Um painel pro dono da fábrica que contratou o Optimize — diferente das telas do app desktop
(pensadas pra quem opera o dia a dia). Ele vê:

- Um dashboard com os dados de Impressoras/Máquinas/Histórico/Reposição/Pedidos/Ordens de
  Serviço (os mesmos seis domínios já portados no app desktop, §22 do documento grande).
- Uma tela de **cadastro de usuários** do próprio Optimize (login+senha, não é conta do
  Windows), com os módulos liberados escolhidos na hora do cadastro. Plano padrão: até 7
  usuários; acima disso, cobrança por usuário extra.
- Uma tela de **faturamento**: quanto o proprietário deve ao Optimize por mês, e a data de
  vencimento da licença.

## Decisão de escopo (confirmada com o usuário, 10/09/2026)

Painel **por fábrica** — cada instalação do Optimize mostra o painel só com os dados daquela
fábrica, pro dono dela. Não é um painel central onde a Optimize (a empresa) gerencia todos os
clientes de uma vez — isso seria um projeto bem maior (backend central, multi-tenant de
verdade, sincronização entre instalações) e não é o que foi pedido.

Consequência: o painel reaproveita o servidor embutido que já existe (`OptimizePro.Servidor`,
Kestrel+SignalR rodando dentro do `Optimize.App`, ver §22.2) — é o mesmo motivo que já tinha
levado a tornar o app cliente-servidor ("várias pessoas vendo o painel ao mesmo tempo").

## Ordem de porte

1. **Usuários + autenticação no backend** ✅ concluído (10/09/2026) — ver §23.1 abaixo.
2. **Faturamento no backend** ✅ concluído (10/09/2026) — ver §23.2 abaixo.
3. **Central de sincronização** ✅ concluído (10/09/2026) — ver §24 abaixo. Reordenado pra
   entrar aqui (pedido do usuário, 10/09/2026): o painel também precisa ser visto de fora da
   rede da fábrica, o que exige uma peça hospedada centralmente antes de fazer sentido montar
   o frontend.
4. **Sincronizador dentro do app desktop** ✅ concluído (10/09/2026) — ver §24.3 abaixo.
5. **Scaffold do frontend em React + tela de login** ✅ concluído (10/09/2026) — ver §24.4/§24.5 abaixo.
6. Telas de dashboard (os seis domínios, versão só-leitura pro proprietário).
7. Tela de cadastro/gestão de usuários.
8. Tela de faturamento.

## §24 — Central de sincronização

> Decisão do usuário (10/09/2026): o painel deve poder ser acessado de fora da rede da
> fábrica. Entre um túnel/relé ao vivo (dados instantâneos, mas uma peça de infraestrutura
> sempre-no-ar mais delicada de manter) e sincronização periódica pra nuvem (dados com um
> pequeno atraso, mas muito mais simples e robusto), o usuário escolheu **sincronização
> periódica**. Banco escolhido: **PostgreSQL** (portátil, roda em qualquer VPS/cloud).
> Provisionamento: automático, junto da ativação da licença — sem passo manual pro cliente.

Isso introduz uma peça nova que as fases 1-2 não tinham: um backend hospedado **fora** do PC
do cliente, que agora sim é multi-tenant de verdade (várias fábricas mandam dados pra cá). É
um projeto à parte de `OptimizePro.Painel` (que continua sendo só o schema local, por
instalação) — a Central é quem os enxerga todos juntos.

### 24.1 Passo concluído (10/09/2026) — projeto + provisionamento de instalação

- Projeto novo **`OptimizePro.Central`** (`Microsoft.NET.Sdk.Web`, diferente do
  `OptimizePro.Servidor` embutido — este é hospedado separadamente de verdade). Só referencia
  `OptimizePro.Core`. PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`.
- `Instalacao` — o tenant: Id, `ClienteIdHash` (o mesmo hash de 32 bits que já vem embutido
  na licença, `OptimizePro.Licenciamento.CodificadorDeLicenca` — reaproveitado como
  identificador natural, ninguém digita nada novo), hash da chave de API (nunca a chave em
  si), nome da fábrica, quando foi criada e a última sincronização.
  - **Limite conhecido**: `ClienteIdHash` é só 32 bits — colisão entre dois clientes
    diferentes é improvável na escala atual, mas não é criptograficamente impossível. Se a
    base de clientes crescer muito, vale revisitar (ex.: um código adicional atribuído por um
    humano na hora do provisionamento).
- `ChaveDeApi` — gera um segredo aleatório de 256 bits, guarda só o hash SHA-256 (não é senha
  de pessoa, é alta entropia — não precisa de PBKDF2 como `OptimizePro.Painel.HashDeSenha`).
- `EstadoDaLicenca` (em `OptimizePro.Services`, código já existente) ganhou o campo
  `ClienteIdHash` — mudança pequena e aditiva (posição opcional no record, testes existentes
  não quebraram) pra dar ao app desktop o que ele precisa passar na hora de provisionar.
- `InstalacaoRepository`/`InstalacaoService` — `ProvisionarAsync` é **idempotente**:
  reprovisionar o mesmo `ClienteIdHash` acha a instalação já existente em vez de duplicar, e
  nunca reemite a chave de API (ela só existe em texto puro no instante da criação — perdeu,
  perdeu; regenerar chave fica pra quando precisar). `AutenticarAsync` só confere identidade
  (não muda nada no banco); `RegistrarSincronizacaoAsync` é separado, pra fase 4 chamar depois
  de processar uma sincronização de verdade.
- Endpoint `POST /api/instalacoes/provisionar` — recebe `ClienteIdHash`+nome da fábrica,
  devolve `instalacaoId` + `chaveDeApi` (só na primeira vez) + `jaExistia`.
- Migração `InicialCentral` gerada (tipos Postgres: `bigint`, `bytea`, `timestamp with time
  zone`) e verificada.
- 9 testes novos (`tests/OptimizePro.Central.Tests`, com um repositório falso em memória —
  sem Postgres real disponível neste ambiente de desenvolvimento, os testes cobrem a lógica de
  negócio; o mapeamento EF Core/Npgsql já foi conferido pela migração gerada corretamente).
  533 testes passando no total do repositório.
- **Testado ponta a ponta contra um Postgres real (Supabase, 10/09/2026)**: o usuário criou um
  projeto no Supabase pra irmos testando. A connection string mora só em
  `dotnet user-secrets` (dentro de `OptimizePro.Central`, chave `ConnectionStrings:Central`) —
  nunca em `appsettings.json`, então nunca vai pro git. Dois detalhes de configuração que
  valeram a pena registrar:
  - `CentralDbContextFactory` (design-time, usado por `dotnet ef`) precisou ler a MESMA
    configuração que `Program.cs` (appsettings + user-secrets + variáveis de ambiente) — a
    versão inicial tinha uma connection string de exemplo hardcoded, e o `dotnet ef database
    update` ignorava o `Program.cs`/user-secrets e tentava essa string fake. Corrigido
    lendo via `ConfigurationBuilder` dentro da própria factory.
  - A porta 6543 do Supabase é o **pooler PgBouncer em modo transação** — por isso a
    connection string usa `Pooling=false` do lado do Npgsql (o PgBouncer já faz o pooling; um
    segundo pool por cima só causa dor de cabeça) e `SSL Mode=Require;Trust Server
    Certificate=true` (Supabase exige TLS).
  - Migração `InicialCentral` aplicada de verdade (`dotnet ef database update`) — a tabela
    `instalacoes` existe no Supabase.
  - `POST /api/instalacoes/provisionar` chamado duas vezes com o mesmo `ClienteIdHash`: a
    primeira criou a linha e devolveu uma chave de API; a segunda achou a mesma linha
    (`jaExistia: true`) e não reemitiu chave — idempotência confirmada contra o banco real, não
    só contra o fake em memória dos testes automatizados.
  - Ficou uma linha de teste na tabela `instalacoes` do Supabase (`ClienteIdHash = 424242`,
    "Fabrica Teste Supabase") — dado de teste inofensivo, não apaguei porque o banco é
    justamente pra isso ("banco pra irmos testando e avançando"); apague quando quiser.

Falta ainda decidir/documentar onde a Central de fato vai rodar em produção (hospedagem é
responsabilidade do usuário, fora do que este ambiente de desenvolvimento consegue
provisionar) — o banco (Supabase) já existe; falta hospedar a API em si.

### 24.2 Passo concluído (10/09/2026) — endpoint de sincronização

- `DadoSincronizado` — em vez de espelhar cada tabela operacional
  (Maquina/RegistroDeImpressao/Pedido/OrdemDeServico/Usuario/ConfiguracaoDeFaturamento) com
  schema+migração própria na Central, guarda cada item como **JSON** (coluna `jsonb` nativa do
  Postgres, com índice) numa tabela genérica `dados_sincronizados`, chaveada por
  (instalação, tipo, id da entidade). A Central não é o sistema de registro de nada disso — é
  só um cache de leitura pro painel remoto; schema mudando do lado local não vira migração
  aqui. Upsert idempotente: o app desktop sempre reenvia o estado inteiro do item.
- `POST /api/sync/lote` — autenticado por dois headers (`X-Instalacao-Id`+`X-Chave-Api`, não
  Bearer/Basic: não há sessão nem usuário humano aqui, só instalação-pra-instalação). Migração
  `AdicionaDadosSincronizados` aplicada no Supabase.

### 24.3 Passo concluído (10/09/2026) — sincronizador dentro do app desktop

Projeto novo **`OptimizePro.Sincronizacao`** — ao contrário de `Painel`/`Central` (isolados de
propósito), este referencia os três lados (`OptimizePro.Data`, `OptimizePro.Painel`,
`OptimizePro.Services`) porque o trabalho dele é justamente fazer a ponte entre os domínios
locais e a Central hospedada.

- `ArmazenamentoDeSincronizacao` — guarda `InstalacaoId`+`ChaveDeApi` localmente, DPAPI-
  protegido, mesmo mecanismo que `LicencaService` usa pro estado da licença (novo arquivo
  `sincronizacao.dat`, ao lado de `licenca.dat`).
  `IClienteCentralHttp`/`ClienteCentralHttp` — fala HTTP com a Central; a URL vem da variável
  de ambiente `OPTIMIZE_CENTRAL_URL` (sem ela, `Configurado` fica `false` e nada é enviado —
  fail-open, igual à checagem de revogação online do licenciamento). Interface extraída só
  pra dar pra testar `SincronizacaoService` sem precisar de um servidor HTTP de verdade.
- `SincronizacaoService.SincronizarAsync` — o fluxo completo:
  1. Se ainda não provisionado localmente, e a licença estiver liberada (usa o
     `ClienteIdHash` que o §24.1 acrescentou a `EstadoDaLicenca`), chama
     `POST /api/instalacoes/provisionar` e salva o resultado. Sem licença liberada ou sem
     internet, não faz nada nesta rodada (tenta de novo na próxima).
  2. Coleta os dados locais em DTOs enxutos (nunca as entidades do EF direto — corta
     navegações e campos que não deviam ir num JSON sincronizado a cada poucos minutos) e
     empurra num lote só via `POST /api/sync/lote`.
  - **Gaps conscientes de escopo, documentados no código**: (a) sempre reenvia o estado
    **inteiro** de cada item a cada ciclo (sem incremental/delta por enquanto — simples e
    correto, revisitar se o volume doer); (b) Histórico sincroniza só os últimos 90 dias (não
    é arquivo morto, é contexto recente pro dashboard); (c) Ordens de Serviço sincronizam sem
    os bytes das imagens (só a contagem) — imagem em si fica pra quando o dashboard remoto
    precisar mostrá-la de verdade, com endpoint próprio; (d) se a Central já conhece o
    `ClienteIdHash` mas o arquivo local `sincronizacao.dat` sumiu (reinstalação, por exemplo),
    não tem como recuperar a chave sozinho — falta uma rota de "regenerar chave" na Central.
- `SincronizadorEmSegundoPlano` (`BackgroundService`, mesmo padrão do
  `PollingDeImpressorasService`) — roda a cada 10 minutos, cria seu próprio escopo de DI a
  cada rodada, falha **sempre** em silêncio (sincronização nunca pode derrubar o app nem gerar
  ruído pro usuário).
- Wiring em `Optimize.App/App.axaml.cs`: `PainelDbContext` registrado ali pela primeira vez
  (schema+migração próprios, mesmo `dados.db`) e migrado junto do `OptimizeDbContext` na
  subida. **Achado importante**: o `IHost` do app nunca tinha sido iniciado
  explicitamente (`_host.Start()`) — não precisava, porque não havia nenhum
  `IHostedService` registrado antes. Sem essa chamada, o `SincronizadorEmSegundoPlano`
  nunca dispararia (ficaria só registrado no container de DI, sem rodar). Corrigido: `_host.
  Start()` logo após montar o host, e `_host.StopAsync()` antes de `Dispose()` no
  desligamento, pra parar os hosted services de forma correta.
- 9 testes novos (`tests/OptimizePro.Sincronizacao.Tests`) — usando um banco SQLite em memória
  com os DOIS DbContexts (operacional + painel) sobre a mesma conexão via `Migrate()` (não
  `EnsureCreated()`, que não funciona bem com dois contextos compartilhando um banco) e uma
  licença de teste ativada de verdade (mesma chave de demonstração de `LicencaServiceTests`).
  Cobre: sem licença não provisiona nem envia nada; cliente não configurado não tenta nada;
  primeira sincronização provisiona e salva o estado local; já provisionado não reprovisiona;
  provisionamento sem chave nova (gap (d) acima) não envia nada; e o caso completo — Máquinas,
  Histórico, Pedidos (com os itens dentro do JSON), Ordens de Serviço, Usuários e Faturamento
  todos coletados e enviados num lote só. 542 testes passando no total do repositório.
- **Testado ponta a ponta contra o Supabase de novo**: subi a Central local apontando pro
  Supabase, provisionei uma instalação de teste (`ClienteIdHash = 777777`) e chamei
  `POST /api/sync/lote` direto (simulando o que o sincronizador do app manda) com uma chave de
  API válida — aceito (`recebidos: 1`). Com uma chave errada — `401`, confirmando que a
  autenticação da fase 4.2 realmente barra quem não tem a chave certa. Também confirmei que o
  app desktop sobe normalmente com o `PainelDbContext` novo sendo migrado (tabela
  `painel_usuarios` criada no `dados.db` local, sem erro nenhum no log).
  - Ficou mais uma linha de teste no Supabase (`instalacoes.ClienteIdHash = 777777`, "Fabrica
    Teste Sync") e uma linha em `dados_sincronizados` (tipo "maquina", id "m1") — dados de
    teste inofensivos, à vontade pra apagar.

### 23.1 Passo 1 concluído (10/09/2026) — usuários + autenticação

Projeto novo **`OptimizePro.Painel`** (biblioteca de classes, `net10.0`) — deliberadamente
isolado: só referencia `OptimizePro.Core` (nunca `OptimizePro.Data`/`Services`/`Servidor`, que
são do domínio operacional). Schema e histórico de migrations próprios
(`PainelDbContext`, tabela `__EFMigrationsHistory_Painel`), no mesmo arquivo `dados.db` da
instalação (mesma fábrica, um proprietário só) mas sem compartilhar `DbContext` com o resto —
evoluir um lado não arrisca o outro.

**O que foi construído:**

- `Usuario` — Id, Login, Nome, SenhaHash+SenhaSal (PBKDF2), `EhAdministrador` (vê
  Usuários/Faturamento independente dos módulos liberados), `Habilitado`, `ModulosLiberados`
  (lista de `ModuloDoPainel`, guardada como JSON), `CriadoEm`/`AtualizadoEm`/`UltimoLoginEm`.
- `ModuloDoPainel` (enum) — os seis domínios liberáveis: Impressoras, Maquinas, Historico,
  Reposicao, Pedidos, OrdensDeServico.
- `HashDeSenha` — PBKDF2-HMACSHA256 (210.000 iterações, recomendação OWASP 2023), sal
  aleatório de 16 bytes por usuário, comparação em tempo constante
  (`CryptographicOperations.FixedTimeEquals`). Sem ASP.NET Identity de propósito — só a
  primitiva de hash, isolada, no mesmo espírito de como o licenciamento usa DPAPI só onde
  precisa.
- `PainelDbContext` + `UsuarioRepository` (CRUD completo: criar, listar, obter por
  id/login, contar habilitados — base do plano de até 7 —, atualizar dados/módulos/senha/
  habilitado, excluir).
- `UsuarioService` — a fachada que a tela vai usar: nunca expõe hash/sal, valida campos
  obrigatórios, lança `LoginJaExisteException` (mensagem amigável) em vez de deixar a
  constraint `UNIQUE` do banco vazar um erro de SQL pra tela.
- `AutenticacaoService` — confere login+senha, mensagem **genérica** tanto pra login
  inexistente quanto pra senha errada (não dar pista de qual dos dois está errado), recusa
  usuário desabilitado, registra `UltimoLoginEm` no sucesso.
- Migração `InicialPainel` gerada e verificada.
- 17 testes novos (`tests/OptimizePro.Painel.Tests`): hash determinístico/aleatório por sal,
  senha nunca aparece em claro no hash/sal, cadastro com validação e duplicidade de login,
  contagem de habilitados, atualização de módulos/admin, redefinição de senha (e que a senha
  antiga para de funcionar), exclusão, autenticação (sucesso, senha errada, login inexistente,
  usuário desabilitado). 516 testes passando no total do repositório.

**Ainda não existe** (fica pras próximas fases): nenhum endpoint HTTP ainda expõe isso — o
`ServidorDoPainel` do domínio operacional não foi tocado. A fase 3 (scaffold do React) é onde
`PainelDbContext`/os serviços daqui ganham uma API de verdade (provavelmente um novo
`WebApplication` dentro do mesmo `ServidorDoPainel`, ou um host HTTP próprio — decisão em
aberto até chegar lá) e o React passa a chamá-la.

### 23.2 Passo 2 concluído (10/09/2026) — faturamento

- `ConfiguracaoDeFaturamento` — linha única (`Id = 1`) com `ValorBaseMensal`,
  `ValorPorUsuarioExtra` e `LimiteDeUsuariosNoPlano` (padrão 7, decisão do usuário
  09/09/2026 revisitada em 10/09/2026). Tabela `painel_faturamento`, migração
  `AdicionaFaturamento`.
  - **"Vence em" propositalmente não mora aqui**: já existe em
    `LicencaService.ObterEstado().ValidoAte` (`OptimizePro.Services`, fora do alcance deste
    projeto por desenho — ver a nota de isolamento no início deste documento). Quem vai juntar
    os dois pra tela de Faturamento é a camada de API na fase 3, que pode referenciar os dois
    lados sem que nenhum dos dois precise referenciar o outro.
- `ConfiguracaoDeFaturamentoRepository` — `ObterAsync` nunca retorna null: sem configuração
  gravada ainda, devolve o padrão (tudo zerado, limite 7) sem escrever nada no banco; só grava
  de verdade quando alguém chama `SalvarAsync`.
- `FaturamentoService` — `ObterConfiguracaoAsync`/`AtualizarConfiguracaoAsync` (com validação:
  nenhum valor negativo, limite de usuários pelo menos 1) e `CalcularMensalidadeAsync`, que
  junta a configuração com `IUsuarioRepository.ContarHabilitadosAsync()` (só usuário
  **habilitado** conta como ocupando vaga do plano — desabilitar alguém libera a vaga na
  hora) e devolve `ResumoDeFaturamento` (base + usuários extras × valor por extra = total).
- 8 testes novos: padrão sem configuração, salvar e reler, validação de valores inválidos,
  cálculo dentro do limite (só cobra a base), cálculo acima do limite (cobra o extra), e que
  usuário desabilitado não conta como extra. 524 testes passando no total do repositório.

### 24.4 Passo concluído (10/09/2026) — login do painel remoto na Central

- `Instalacao.Codigo` — código curto de 6 caracteres (alfabeto sem 0/O/1/I/L, pra não
  confundir quem digita), gerado no provisionamento, único. O Id é um GUID — ninguém decora
  isso; o proprietário decora "empresa: XKMXVC". Migração `AdicionaCodigoDaInstalacao`
  (com um passo de dado pra preencher as linhas de teste já existentes sem colidir no índice
  único novo) aplicada no Supabase.
- `VerificacaoDeSenha` — mesmo algoritmo/parâmetros de `OptimizePro.Painel.HashDeSenha`
  (PBKDF2-HMACSHA256, 210.000 iterações), duplicado de propósito (isolamento) mas
  **precisa ficar idêntico**: confere o mesmo hash que o app desktop gerou e sincronizou.
- `AutenticacaoDeUsuarioService` — login remoto é (código da empresa, login, senha), diferente
  de `IInstalacaoService.AutenticarAsync` (que confere a chave de API da sincronização
  máquina-a-máquina). Procura o `Usuario` dentro do espelho JSON (`DadoSincronizado` tipo
  "usuario") da instalação daquele código, confere a senha, recusa usuário desabilitado —
  mesma mensagem genérica de erro pra código/login/senha errados (não dar pista de qual dos
  três está errado).
- `EmissorDeToken` — JWT assinado (HMAC-SHA256, chave em `Jwt:ChaveSecreta` via user-secrets,
  validade 12h) com os módulos liberados e "é administrador" já dentro dos claims — o React
  monta o menu sem precisar de outra chamada. `POST /api/auth/login` (emite o token) e
  `GET /api/auth/me` (protegido, confere se um token guardado ainda vale).
  - **Achado testando de verdade contra o Supabase** (não aparece em teste unitário que só
    olha o JWT cru): sem `o.MapInboundClaims = false` na configuração do `AddJwtBearer`, o
    ASP.NET Core remapeia silenciosamente `"sub"`/`"name"` pros URIs longos de `ClaimTypes`
    (herança do WS-Federation) — toda leitura por `JwtRegisteredClaimNames` em `/api/auth/me`
    vinha `null`. Corrigido; documentado no código pra não se perder de novo.
- 11 testes novos (`CodigoDaInstalacaoTests`, `EmissorDeTokenTests`,
  `AutenticacaoDeUsuarioServiceTests` — incluindo isolamento entre duas instalações com o
  mesmo login e senhas diferentes). 553 testes passando no total do repositório.
- **Testado ponta a ponta contra o Supabase**: provisionou uma instalação, sincronizou um
  usuário (hash gerado em Node, compatível byte a byte com o PBKDF2 do .NET — confirma que o
  formato é interoperável de verdade, não só "parece igual"), logou com `/api/auth/login`,
  confirmou os claims certos em `/api/auth/me` com o token, e confirmou que senha errada e
  requisição sem token voltam 401.

### 24.5 Passo concluído (10/09/2026) — scaffold do React

Projeto novo **`painel-web`** (Vite + React 19 + TypeScript, fora da solução .NET — é um
projeto Node à parte, na raiz do repositório). CORS liberado na Central pra ele chamar de
outra origem (a Central só serve API, sem cookie de sessão — não há CSRF a se preocupar).

- `src/api/central.ts` — cliente HTTP simples (fetch, sem lib), `login()`/`obterUsuarioAtual()`.
- `src/auth/AuthContext.tsx` — sessão (token+usuário) guardada em `localStorage`; ao carregar a
  página, confere contra `/api/auth/me` se o token ainda vale antes de considerar a pessoa
  logada (um token vencido não deve parecer uma sessão válida até a primeira chamada real
  falhar).
- `src/pages/Login.tsx` — empresa (código)/login/senha.
- `src/pages/Painel.tsx` — casca autenticada mínima: nome de quem entrou, botão Sair, lista
  dos módulos liberados (+ Usuários/Faturamento se for administrador). As telas de verdade de
  cada módulo (dashboard, listas, indicadores) são a fase 6 — isto só prova que login+sessão
  funcionam de ponta a ponta.
- **Achado corrigido**: o template mais novo do Vite (react-ts) vem com
  `erasableSyntaxOnly` no `tsconfig`, que proíbe o açúcar de "parameter properties" do
  TypeScript (`constructor(public status: number)`) — precisou virar campo + atribuição
  explícita em `ErroDaApi`.
- **Testado de verdade num navegador real** (não só `npm run build`): subiu a Central local
  (apontada pro Supabase) e o `npm run dev`, abriu `http://localhost:5173` no Chrome,
  preencheu o formulário com uma instalação/usuário sincronizados de verdade, clicou Entrar —
  logou, mostrou os módulos certos, e um F5 depois manteve a sessão (confirma o fluxo de
  `/api/auth/me` na inicialização, não só o login em si).
