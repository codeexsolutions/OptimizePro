# Estrutura do Optimize

> Para "onde eu mexo para mudar X?", veja **[MAPA.md](MAPA.md)** — a visão de
> cima, as camadas e as armadilhas já pagas.

O sistema fica dividido em código do servidor, painel web e arquivos gerados em execução.

## Código do servidor

- `server.js`: inicialização do Express, Socket.IO e WhatsApp.
- `db.js`: banco dos moldes e da memória do encaixe.
- `moldes-api.js`: operações da área de moldes.
- `uploads-arquivos.js`: o que moldes e projetos têm em comum ao guardar imagem em disco.
- `projetos-api.js`: projetos de cliente — a pasta, o projeto e as peças já prontas.
- `encaixe-memoria.js` e `encaixe-pdf.js`: cálculo, memória e documento do encaixe.

## Painel web

- `public/index.html`: estrutura das telas e modais.
- `public/style.css`: estrutura funcional de cada recurso — grade, tabelas, formulários.
- `public/interface.css`: a identidade Optimize. **Toda cor da interface sai dos tokens declarados aqui**; escrever um hex em qualquer outro arquivo quebra o tema.
- `public/geometria.js`: as contas de contorno (área, caixa, simplificação) que Moldes, Vetor e Encaixe dividem.
- `public/interface.js`: menu lateral e relógio.
- `public/app.js`: conexão, disparo e navegação entre telas.
- `public/projetos.js`: tela de Projetos — a estante por cliente e o editor.
- demais arquivos de `public/`: telas especializadas de moldes, encaixe e vetor.

## Padrões de interface

- **Cabeçalho de cartão** (`.card-head`): título, uma linha de apoio e a ação principal do cartão na mesma altura. Substitui o `style=` na marra que cada tela usava antes.
- **Ajuda sob demanda** (`.ajuda`, um `<details>`): o texto que explica o porquê fica fechado atrás de um "?". É longo e quase sempre já é sabido — aberto, empurrava o formulário para baixo da dobra.
- **Número do passo** (`.passo-selo`): Encaixe e Disparo são sequências; ver "1, 2, 3" descendo a página evita a dúvida de por onde começar.

## Moldes e Projetos não são a mesma coisa

As duas telas guardam trabalho para reaproveitar, mas o que elas guardam é diferente, e confundir uma com a outra leva a modelar errado:

- **Moldes** guarda a **geometria** da peça (o contorno em centímetros). A estampa é aplicada nele depois, e o mesmo molde serve para P, M e G.
- **Projetos** guarda a **arte já aplicada** — a estampa na camisa, na bandeira. Não há passo seguinte: a peça vai direto para o encaixe.

Por isso o projeto guarda também os ajustes do encaixe (largura do tecido, folga, margem, giro): repetir um pedido é abrir, dizer quantas unidades e mandar calcular.

## Arquivos que não são código-fonte

`node_modules/`, `dados.db*`, `uploads/`, `.wwebjs_cache/` e as pastas de backup são dados locais ou arquivos gerados. Eles continuam preservados, mas ficam fora do controle de versão pelo `.gitignore`.

As tabelas de Projetos se chamam `projeto_clientes`, `projetos` e `projeto_pecas`. O nome não é `clientes` de propósito: um `dados.db` antigo ainda tem a tabela `clientes` do módulo comercial que saiu, e um `CREATE TABLE IF NOT EXISTS clientes` não criaria nada — o código passaria a ler a tabela velha, com as colunas erradas.

O `dados.db` de instalações antigas ainda guarda as tabelas do módulo comercial e financeiro (clientes, produtos, produções, lojas, notas, pagamentos, orçamentos e configurações). Elas não são mais criadas nem lidas por nenhum código; ficam ali só como registro, caso um dia seja preciso recuperar aqueles dados. O mesmo vale para as imagens em `uploads/artes/`.

## Regra para novas telas

O HTML permanece em `public/index.html`. A regra da tela deve entrar no arquivo JavaScript do recurso correspondente. Mudanças puramente visuais devem ficar em `public/interface.css`, evitando misturar aparência com banco ou integrações.
