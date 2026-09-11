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
6. **Telas de dashboard** ✅ concluído (10/09/2026) — ver §24.6 abaixo.
7. **Tela de cadastro/gestão de usuários** ✅ concluído (10/09/2026) — ver §24.7 abaixo. Essa
   fase mudou quem escreve `Usuario`: passou a ser a Central (não mais o desktop) — ver §24.7
   pra entender por quê.
8. **Tela de faturamento** ✅ concluído (10/09/2026) — ver §24.8 abaixo.

Todas as 8 fases do painel do proprietário concluídas.

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

### 24.6 Passo concluído (10/09/2026) — telas de dashboard

- `DashboardService` (Central) — lê só o espelho JSON já sincronizado (`DadoSincronizado`),
  nunca fala com o PC da fábrica. Pra Máquinas/Histórico/Pedidos/Ordens de Serviço, é
  desserializar as linhas do tipo certo; **Impressoras** e **Reposição** são derivados:
  - **Impressoras**: junta Máquinas com o Histórico de hoje (trabalhos e metragem por
    máquina, último trabalho) — sem status ao vivo (isso nunca foi sincronizado, decisão do
    §24.2: só dado periódico, não túnel em tempo real).
  - **Reposição**: mesmo porte de `OptimizePro.Services.Impressoras.Historico.ReposicaoService`
    (semana de segunda a domingo, reconhece "reposição" no nome sem acento/caixa) —
    duplicado de propósito na Central, mas com a mesma lógica exata.
- 6 endpoints `GET /api/dashboard/{maquinas,impressoras,historico,reposicao,pedidos,
  ordens-servico}`, cada um exigindo o módulo correspondente no token (`RequireAuthorization`
  + checagem do claim `modulo_liberado`) — **não** é "administrador vê tudo": módulo
  operacional é concedido por si só, independente de `EhAdministrador` (que só dá acesso a
  Usuários/Faturamento). Testado que um usuário sem "Maquinas" liberado toma 403 no endpoint
  de máquinas mesmo sendo administrador.
- 6 testes novos de `DashboardService` (desserialização, linha corrompida não derruba as
  outras, agrupamento de Reposição, derivação de Impressoras, isolamento entre instalações
  diferentes). 560 testes passando no total do repositório.
- **React**: `src/api/dashboard.ts` (tipos TS confirmados contra o JSON real da Central —
  minimal API usa camelCase por padrão, diferente do PascalCase salvo dentro do blob JSON no
  Postgres), `src/hooks/useDados.ts` (o mesmo padrão carregando/erro/dados repetido nas 6
  telas, só isso), `src/layout/Layout.tsx` (barra lateral construída a partir de
  `modulosLiberados` da sessão — cada pessoa só vê o que tem liberado), e as 6 páginas em
  `src/pages/dashboard/`. `App.tsx` manda pra primeira rota liberada da pessoa (não trava
  todo mundo em "/maquinas" se o módulo dela for outro).
- **Testado ponta a ponta com dados reais no Supabase**: sincronizou máquina, histórico,
  pedido (com itens) e ordem de serviço (com "Confecção Sol", acento incluso) de verdade,
  logou no painel com todos os 6 módulos liberados, confirmou visualmente as telas
  Impressoras (cartão com trabalhos/metragem/último trabalho corretos) e Ordens de Serviço
  (tabela com os dados certos, acentuação preservada ponta a ponta) — provando que o pipeline
  inteiro funciona: app desktop → Central (Postgres) → React, sem atalho nenhum no meio.

### 24.7 Passo concluído (10/09/2026) — gestão de usuários (a Central vira fonte de verdade de Usuario)

> Pedido do usuário: "será possível no painel alterar o mostrar ou não o módulo que o usuário
> verá na instalação dele" — cadastro E edição de módulos liberados, direto no painel remoto.

**Decisão de arquitetura que essa fase forçou**: até aqui, `Usuario` era sincronizado só numa
direção (desktop → Central, dentro do lote periódico do §24.2). Se o painel remoto (React)
também pudesse editar módulos, o próximo ciclo de sincronização do desktop simplesmente
sobrescreveria a edição de volta ao estado antigo — o desktop nunca soube que algo mudou do
lado de fora. Resolvido assim: **a partir desta fase, `TipoDeDadoSincronizado.Usuario` é escrito
só pela Central** (direto, via `/api/usuarios`), e o app desktop **parou de empurrar Usuario**
no lote (`SincronizacaoService.ColetarUsuariosAsync` removido). Isso foi possível sem precisar
de sincronização reversa porque o app desktop nunca teve tela própria de cadastro de usuário —
a tabela local (`OptimizePro.Painel.Usuarios`) nunca teve dado real de produção, só usuários de
teste manuais durante o desenvolvimento. As outras 5 entidades (máquina/histórico/pedido/
OS/faturamento) continuam de leitura exclusiva aqui, escritas só pelo desktop — não mudou nada
nelas.

- `OptimizePro.Central.HashDeSenha` — renomeado de `VerificacaoDeSenha` e ganhou `Gerar()`
  (mesmo PBKDF2-HMACSHA256/210k iterações de `OptimizePro.Painel.HashDeSenha`): antes a Central
  só verificava senha gerada pelo desktop, agora também gera senha nova.
- `IDadoSincronizadoRepository` ganhou `ObterAsync`/`SalvarAsync`(item único)/`ExcluirAsync` —
  o repositório genérico de sincronização (§24.2) agora também serve de armazenamento de
  escrita direta da Central, não só de upsert em lote vindo do desktop.
- `UsuarioAdminService` — cadastra (`CadastrarAsync`), edita nome/módulos/administrador
  (`AtualizarAsync`), redefine senha (`RedefinirSenhaAsync`), ativa/desativa
  (`AtualizarHabilitadoAsync`) e exclui (`ExcluirAsync`) usuários, tudo em cima de
  `DadoSincronizado(Tipo="usuario")`. **Sem limite de 7 bloqueando o cadastro** — o plano
  padrão de 7 usuários é uma regra de cobrança (Faturamento, fase 8), não uma trava de
  cadastro: o dono pode ter mais de 7, só paga mais por isso.
- **Problema do "ovo e a galinha" resolvido com bootstrap de abertura única**: como só
  administrador pode chamar `/api/usuarios` (exige JWT), e só existe JWT depois de logar, e só
  dá pra logar se já existir um usuário — nada conseguiria criar o primeiro usuário de uma
  instalação nova. `POST /api/usuarios/bootstrap` (Código da instalação + login/nome/senha, sem
  token) resolve isso: só funciona enquanto a instalação tiver zero usuários; cria o primeiro
  como administrador com todos os 6 módulos liberados; depois disso fecha pra sempre
  (`JaTemUsuarioException`, HTTP 409) — não é uma porta permanente, é só a única forma possível
  de sair do zero.
- Endpoints (`RequireAuthorization` + checagem de `eh_administrador`, nunca módulo — gestão de
  usuário não é módulo operacional): `GET/POST /api/usuarios`, `PUT /api/usuarios/{id}`,
  `POST /api/usuarios/{id}/redefinir-senha`, `PUT /api/usuarios/{id}/habilitado`,
  `DELETE /api/usuarios/{id}`. Guardas de autoproteção: ninguém consegue tirar o próprio
  "administrador", se autodesativar ou se autoexcluir (evita trancar a única conta admin pra
  fora do próprio painel de usuários).
- `IDadoSincronizadoRepository`/`RepositorioDeDadoSincronizadoFalso` (fake de teste) e 8 testes
  novos de `UsuarioAdminService` (cadastro com hash, login duplicado, mesmo login em
  instalações diferentes não conflita, edição de módulos, redefinição de senha, ativar/
  desativar, excluir, bootstrap e o fechamento do bootstrap). 37 testes no projeto de testes da
  Central (era 27).
- `OptimizePro.Sincronizacao`: `ColetarUsuariosAsync` removida, teste de integração ajustado
  pra confirmar que "usuario" **não** aparece mais no lote enviado (só
  maquina/registro_impressao/pedido/ordem_servico/faturamento). 9 testes continuam passando.
- **React**: `src/api/usuarios.ts` (cliente CRUD completo), `src/pages/admin/Usuarios.tsx` —
  formulário de cadastro com checkboxes de módulo, lista com edição inline (nome, módulos via
  checkbox, redefinir senha opcional, administrador), ativar/desativar e excluir com
  confirmação; rota `/usuarios` protegida por `RotaDeAdministrador` (além do 403 que a própria
  Central já devolve pra quem não é administrador).
- **Testado ponta a ponta com dados reais no Supabase** (via curl, não houve automação de
  browser disponível neste ambiente pra clicar na UI): provisionou instalação nova, bootstrap
  do primeiro admin, confirmou que bootstrap fecha (409 na segunda tentativa), login, criou um
  operador com 1 módulo liberado, **editou os módulos liberados dele pra outro conjunto**
  (o pedido central desta fase) e confirmou a mudança na listagem, desativou, confirmou que
  login de usuário desativado falha, testou as 3 guardas de autoproteção (não desativa/exclui/
  rebaixa a própria conta), e excluiu. `npm run build` (tsc + vite) do React passou limpo.

### 24.8 Passo concluído (10/09/2026) — tela de faturamento (última fase)

Mensalidade devida ao Optimize + "vence em" da licença, na mesma tela — pedido original do
usuário na primeira mensagem desta iniciativa.

- **"Vence em" nunca tinha sido sincronizado pra Central** (decisão consciente das fases
  anteriores — a Central só falava de dado operacional/usuário). Resolvido sem criar uma nova
  entidade sincronizada: `FaturamentoDto` (Sincronizacao) ganhou o campo opcional
  `LicencaValidaAte`, preenchido em `SincronizacaoService.ColetarFaturamentoAsync` a partir de
  `LicencaService.ObterEstado().ValidoAte` (já existia, só não viajava). Viaja junto do
  faturamento porque as duas informações sempre aparecem juntas nesta tela — criar
  `TipoDeDadoSincronizado` só pra uma data seria mais uma entidade pra sincronizar sem
  necessidade real.
- `OptimizePro.Central.FaturamentoSincronizadoDto` — mesma forma, espelhada (isolamento §23/§24).
- `FaturamentoService` (Central) — lê a linha `DadoSincronizado(Tipo="faturamento", EntidadeId="1")`
  da instalação e calcula a mensalidade (mesma fórmula de `OptimizePro.Painel.FaturamentoService`,
  duplicada de propósito): `valorBase + max(0, habilitados - limite) × valorPorUsuarioExtra`.
  **Contagem de usuários habilitados vem de `IUsuarioAdminService`, não do que foi sincronizado
  por último** — desde a §24.7 a Central é quem manda em `Usuario`, então contar por ali reflete
  edições feitas no painel remoto depois do último ciclo de sync do desktop, não um número
  potencialmente desatualizado.
- `GET /api/faturamento` — administrador apenas (mesma regra de `/api/usuarios`: não é módulo
  operacional). Devolve 404 (não erro) quando a instalação ainda não sincronizou nenhum lote
  com faturamento — acontece logo depois de ativar a licença, antes do primeiro ciclo do
  sincronizador; a tela trata isso como "ainda sem dados", não como falha.
- 5 testes novos de `FaturamentoService` (sem dados ainda, dentro do limite, acima do limite
  cobrando por usuário extra, usuário desativado não conta na mensalidade, isolamento entre
  instalações). 42 testes no projeto de testes da Central (era 37).
- **React**: `src/format.ts` ganhou `reais()` (formatação de moeda BRL); `src/api/faturamento.ts`;
  `src/pages/admin/Faturamento.tsx` — dois cartões (mensalidade com o detalhamento de
  usuários extra quando houver, e a validade da licença com selo "Em dia"/"Vence em breve"/
  "Vencida"); rota `/faturamento` protegida por `RotaDeAdministrador` (o link na barra lateral
  já existia desde a §24.6).
- **Testado ponta a ponta com dados reais no Supabase** (via curl — sem automação de browser
  disponível neste ambiente): confirmou 404 antes de qualquer sincronização; simulou o lote
  periódico do desktop via `POST /api/sync/lote` com faturamento + validade de licença; conferiu
  a mensalidade com só o dono habilitado (dentro do limite, sem cobrança extra); criou 8
  usuários extras (9 habilitados no total, limite 7) e confirmou `usuariosExtras=2`,
  `mensalidadeTotal=350` (300 + 2×25) — a fórmula bate exatamente; limpou os usuários de teste
  depois. `npm run build` (tsc + vite) do React passou limpo. Suíte completa do backend:
  238 testes passando (Central 42, Data 28, Painel 25, Services 139, Sincronizacao 9)
  fora os projetos que este trabalho não tocou.

## Fechamento

As 8 fases planejadas do painel do proprietário estão concluídas: cadastro/autenticação de
usuários com módulos por pessoa (editáveis a qualquer momento pelo painel remoto), acesso de
fora da rede da fábrica via sincronização periódica pra uma Central hospedada (Postgres/
Supabase), dashboard dos 6 domínios operacionais, e faturamento com validade de licença. Gaps
conscientemente aceitos ao longo do caminho (documentados nas seções correspondentes): sem
bytes de imagem de OS sincronizados, sem sincronização incremental/delta, `ClienteIdHash` de
32 bits como limite de escala conhecido, chave de licenciamento demo ainda não rotacionada
(ver memória [[licenciamento_chave_demo_pendente]]). Nenhum desses bloqueia uso real — são
decisões de escopo pra revisitar quando (e se) a base de clientes justificar o esforço.

## §25 — Login e gate de módulo no app desktop (extensão pós-fechamento, 10/09/2026)

> Pedido do usuário: se o painel remoto restringe módulo por usuário, o app desktop (Optimize.App)
> deveria respeitar a mesma restrição — evita que um funcionário com pouca permissão no painel
> use o produto completo só porque está fisicamente no PC da fábrica.

**Isso reabre, na direção oposta, a tensão resolvida na §24.7.** Desde lá, a Central é a única
fonte de verdade de `Usuario` (o desktop parou de empurrar isso pra cima). Pra o desktop também
respeitar módulo por usuário, ele precisa de volta um cache local de `Usuario` — mas como
precisa funcionar **offline** (ninguém pode ficar sem abrir o produto por falta de internet),
não dá pra validar login contra a Central a cada vez. Resolvido com uma sincronização na
direção contrária da §24.2: **Central → desktop**, best-effort, no mesmo ciclo periódico.

- `GET /api/sync/usuarios` (Central) — mesma autenticação por chave de API de `/api/sync/lote`
  (instalação-pra-instalação, não é o login humano de `/api/auth/login`). Devolve a lista crua
  de usuários da instalação, **com hash+sal de senha inclusos** — o desktop precisa validar
  senha sozinho, sem rede.
- `IClienteCentralHttp.ObterUsuariosAsync` (Sincronizacao) — GET com os mesmos headers de
  `EnviarLoteAsync`; null em qualquer falha (offline, Central fora do ar), fail-open como todo
  o resto desta camada.
- `SincronizacaoService.PuxarUsuariosAsync` — chamado a cada ciclo de `SincronizarAsync`,
  depois do push das outras 5 entidades. Espelho completo (apaga tudo + recria), não upsert
  incremental — a tabela é pequena e a Central sempre manda o estado inteiro; duas
  `SaveChangesAsync` separadas (delete, depois insert) evita depender de ordem de execução do
  EF quando delete/insert reusam o mesmo Id. Falha no pull não deve derrubar o retorno do push
  (são passos independentes) e **nunca apaga o cache antigo em caso de erro** — só substitui
  quando a Central responde de verdade. Módulo com nome desconhecido no cache (versão do
  desktop desatualizada, por exemplo) é ignorado, não derruba a linha inteira.
- **Gate reaproveita infraestrutura já existente e nunca usada**: `OptimizePro.Painel.Usuario`/
  `IUsuarioRepository`/`IAutenticacaoService` (§23.1) foram construídos na fase 1 do painel mas
  nunca tiveram UI no desktop — agora servem exatamente pro propósito original, só que a tabela
  que eles leem virou espelho da Central em vez de fonte própria.
- `Optimize.App.Services.SessaoDoPainel` — singleton simples (`Usuario? UsuarioAtual`) guardando
  quem logou nesta sessão do processo.
- `LoginDoPainelViewModel`/`LoginDoPainelWindow` — mesma UI/padrão de `LicencaViewModel`/
  `LicencaWindow` (fecha sozinha ao autenticar; `App.axaml.cs` decide o que abrir a seguir).
- **Gate condicional, não obrigatório**: `App.axaml.cs` só mostra a tela de login se
  `PainelDbContext.Usuarios` tiver pelo menos 1 linha localmente. Instalação nova, que nunca
  configurou usuário nenhum no painel remoto (ou nunca sincronizou ainda), continua abrindo
  direto — não trava quem não usa esse recurso. Uma vez que o dono cadastra usuários no painel
  e o desktop sincroniza pelo menos uma vez (ciclo de 10 min), o gate liga sozinho na próxima
  abertura do app.
- `MainWindowViewModel.PodeVer{Impressoras,Maquinas,Historico,Reposicao,Pedidos,
  OrdensDeServico}` — mesmos 6 módulos do painel remoto (`ModuloDoPainel`), únicos que entram
  no gate. **Moldes/Projetos/Encaixe/Vetor/Disparo/Configurações nunca são módulo** — são o
  produto original do Optimize, fora do escopo do painel do proprietário, sempre visíveis
  independente de quem logou. `UsuarioAtual == null` (gate desligado) também mostra tudo —
  mesmo comportamento de sempre, backward-compatible.
- Defesa em profundidade: a checagem de módulo não fica só no `IsVisible` do menu (fácil de
  esquecer um ponto de entrada) — `NavegarPara`, `NavegarParaTipo` e o handler de
  `INavegador.Navegado` (os três jeitos de trocar de tela) todos passam por
  `PodeNavegarPara(tipo)` antes de trocar `TelaAtual`.
- 2 testes novos em `OptimizePro.Sincronizacao.Tests` (`Sincronizar_PuxaUsuariosDaCentral_
  AtualizaCacheLocal`, incluindo módulo desconhecido sendo ignorado sem derrubar a linha;
  `Sincronizar_CentralIndisponivelNoPull_MantemCacheLocalAntigo`). 11 testes no projeto (era 9).
  Sem projeto de testes pro `Optimize.App` ainda — a lógica de gate em si (`PodeVer*`,
  `PodeNavegarPara`) não tem teste automatizado dedicado, só build limpo + a mesma cobertura
  indireta de `AutenticacaoService`/`HashDeSenha` que já existe em `OptimizePro.Painel.Tests`.
- **Testado ponta a ponta com o app desktop de verdade**: gerada uma licença real (chave demo),
  ativada no Optimize.App, sincronizador provisionou uma instalação nova na Central (Supabase),
  bootstrap do admin + cadastro de um usuário `restrito` (só Histórico e Pedidos liberados) via
  `/api/usuarios`. Reaberto o app: a tela de login apareceu (gate ligou sozinho assim que o
  cache local ganhou usuários) e, logado como `restrito`, o menu lateral escondeu Impressoras/
  Máquinas/Reposição/Ordens de Serviço, mantendo só Histórico/Pedidos — e manteve
  Moldes/Projetos/Encaixe/Vetor/Disparo/Configurações sempre visíveis, confirmando visualmente
  (screenshot) que o gate funciona fim a fim: licença → sincronização reversa → cache local →
  login offline → menu filtrado. Suíte completa do backend: 577 testes passando.
  **Gap real encontrado durante o teste** (não é bug de código, é o limite já documentado na
  §24.1): reprovisionar uma instalação já existente via `/api/instalacoes/provisionar` sem ter
  guardado a chave de API da primeira vez devolve `chaveDeApi: null` e não há como recuperar —
  aconteceu comigo mesmo ao inspecionar via curl antes do app ter chance de salvar a própria
  chave. Contornado manualmente nesta sessão de teste (escrevendo o estado local direto);
  o único jeito real de evitar isso em produção é o app sempre ser o primeiro a provisionar,
  sem inspeção manual no meio.
