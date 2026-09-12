# Novidades do optmize-full (11–12/09/2026)

> Levantamento pedido pelo usuário (12/09/2026): "acesse o projeto optmize-full, verifique as
> novas alterações e documente para incluir no projeto optmizePro". Cobre os commits mais
> recentes do `optmize-full` (de `c66b7c8`/`7cf1be0` até `9dbebf0`, HEAD do master em
> 12/09/2026) — uma leva grande batizada de "encaixe-no-servidor" mais alguns recursos
> independentes que entraram junto. Nada disso foi portado ainda; este documento só registra o
> que existe lá e uma recomendação de prioridade, pra decidir com calma o que vale trazer.

## Resumo

| # | Novidade | Vale portar? |
|---|---|---|
| 1 | **Digitalizar** — foto de molde na mesa vira contorno vetorial editável | Sim, é feature nova de verdade — decisão de prioridade pendente |
| 2 | Fitter de curva com detecção de reta (menos nós, reta fica reta) | Junto com #1 — mesmo arquivo/lógica |
| 3 | Encaixe rodando via HTTP + PDF solto no Encaixe vira arte (imagem), não marcador vetorial | A parte de "PDF como arte" merece checar o comportamento atual do OptimizePro; o HTTP em si não se aplica |
| 4 | Licenciamento por token (Ed25519, offline) | Não portar — confirma que a arquitetura do OptimizePro (ECDSA) já está certa |
| 5 | Tela de espera durante o cálculo do Encaixe | Sim, barato e direto — UX pura |
| 6 | Ajustes de marca/sidebar/rota sem "#" | Cosmético, baixa prioridade |
| 7 | Sonda dentro de um CorelDRAW via Docker | Não se aplica — OptimizePro não integra com CorelDRAW |

---

## 1. Digitalizar — foto do molde na mesa vira contorno editável

**O que é.** Tela nova no optmize-full (commit `071f2fd`): a pessoa tira uma foto dos moldes de
papel/tecido espalhados na mesa (ou na esteira da laser), o app acha o contorno de cada peça
sozinho, e deixa arrastar nó por nó pra corrigir onde a detecção errou. A escala real (cm) é
calibrada por UMA medida feita à mão no mundo real — nunca assume nada a partir do DPI da
imagem. No final, exporta PDF em tamanho real (gabarito de corte) e SVG (pra abrir no
CorelDRAW).

**Como funciona.**
1. Binarização por Otsu, mas a **polaridade claro/escuro é inferida pela borda da imagem**, não
   assumida fixa — isso foi o bug real da primeira tentativa: o código reaproveitava a
   `silhuetaDeDados` do Encaixe (pensada pra arte escura em papel branco) e quebrava com molde
   de papel kraft claro sobre esteira escura da laser.
2. Contorno bruto passa por suavização de Chaikin (tira o serrilhado de pixel).
3. Ajuste de curvas de Schneider (`ajusteDeCurvas.js`) reduz ~377 pontos brutos a ~60 nós
   editáveis.
4. Cantos são detectados e o contorno é **cortado ali antes de ajustar a curva** — um entalhe de
   costura (ex.: gancho) não pode virar curva arredondada por acidente. Nó redondo = curva; nó
   quadrado = canto (sem espelhar as alças).

**Relevância pro OptimizePro.** É processamento de imagem client-side + ajuste de curva — não
depende de servidor nem de navegador, então é portável em princípio pra uma tela nova no
Avalonia (SkiaSharp cobriria a parte de imagem; o fitter de Schneider seria um porte de
algoritmo, não uma reescrita de arquitetura). É a única novidade da lista que é uma **feature de
produto nova** de verdade, não infraestrutura — vale decisão explícita de prioridade com o
usuário antes de entrar na fila.

## 2. Fitter de curva com detecção de reta

**O que é.** Melhoria do fitter usado no item 1 (commit `449c0b0`): detecta trechos retos ANTES
de ajustar curva, então uma borda reta de verdade sai como reta (`L`/`lineTo`) em vez de virar
uma curva "achatada" mas tecnicamente curva. Resultado medido: nós por peça caíram de ~42,5 pra
~19,3 em 16 fotos de teste.

Também corrigiu a métrica de erro do ajuste: antes comparava por parametrização de corda (`t`
estimado por distância em linha reta), o que escondia um erro real de 18mm atrás de um erro
reportado de 2 unidades — agora mede por comprimento de arco percorrido, nos dois sentidos.
Elimina nós quase coincidentes (<4 unidades de distância).

**Relevância.** Mesmo arquivo/família de código do item 1 — se portar o Digitalizar, portar os
dois juntos.

## 3. Encaixe rodando via HTTP + PDF como arte no Encaixe

**Por que existe.** O CorelDRAW embute dois controles de navegador diferentes pros próprios
add-ons: um `type="browser"` (IE11 — tem acesso ao documento aberto via
`window.external.Application`, mas sem ES6/WASM) e um `type="browserEdge"` (Chromium moderno,
sem acesso ao documento). Uma sonda dedicada (`corel/OptimizeSonda/AppUI.xslt`, ver item 7)
confirmou que só o IE11 enxerga o desenho — só que ele não roda o motor de encaixe moderno. Daí
a solução: expor o encaixe por HTTP (`POST /api/encaixe/resolver`, novo em `servidor/`) pra esse
IE11 chamar o cálculo de fora. **O algoritmo de encaixe em si não mudou** — só ganhou um jeito
de rodar fora do navegador principal; a tela normal de Encaixe continua calculando client-side
por padrão.

**Mudanças de UX junto** (`738153f`): exportar passou de `<a download>` do navegador pra o
servidor escrever direto numa pasta `exportado/` e abrir o Explorer nela; o campo "tempo de
procura" saiu de perto do botão Otimizar e foi pro painel de confirmação, do lado da Bancada.

**PDF solto no Encaixe agora vira arte, não marcador** (`4c13977`): um PDF arrastado direto pro
Encaixe é **rasterizado como imagem** (fundo transparente, uma imagem = uma peça, via
`pdfParaArte.js`) em vez de ser lido como vetor (que fatiava um PDF de arte em uma peça por
contorno fechado — errado pra esse caso). A tela de Moldes continua lendo PDF como marcador
vetorial normalmente; só o caminho de "solta direto no Encaixe" mudou.

**Relevância pro OptimizePro.** O HTTP em si não se aplica — o OptimizePro é um processo só, sem
consumidor externo tipo "CorelDRAW preso num IE11" pra justificar expor a rede. Mas vale
**conferir o comportamento atual**: como o `LeitorPdf`/molde do OptimizePro trata hoje um PDF
solto direto na tela de Encaixe — se ele tenta ler como vetor (marcador) e fatia por contorno,
pode estar reproduzindo o mesmo problema que o optmize-full acabou de corrigir.

## 4. Licenciamento por token (Ed25519) — não portar, só confirma o rumo

**O que é.** O optmize-full passou a exigir, pela primeira vez, um token assinado
(`OPTMIZE1.<payload base64>.<assinatura>`) colado uma vez na instalação — verificado offline
via **Ed25519** (`servidor/licenca.js`), chave pública embutida, chave privada só no painel
emissor separado. Trava por máquina, ativação única, data de expiração, e um middleware Express
("porteiro") devolve 402 em toda rota da API sem licença.

**Relevância.** É o **mesmo mecanismo**, implementado do zero agora no optmize-full — o
OptimizePro já tem isso pronto e rodando desde antes (`OptimizePro.Licenciamento` +
`LicencaService`, ECDSA/SHA-256 em vez de Ed25519). Não é novidade a portar; é uma confirmação
de que a arquitetura escolhida pro OptimizePro já estava certa. A única pendência real já registrada em
outro lugar é trocar a chave demo do optmize-full antes do lançamento dele — o OptimizePro já
trocou a sua (10/09/2026).

## 5. Tela de espera durante o cálculo do Encaixe

Animação de "peças caindo num poço" enquanto o encaixe calcula (`9dbebf0`) — não é exclusiva do
caminho por HTTP, cobre qualquer cálculo que demore um tempo perceptível. Item barato e direto:
o Encaixe do OptimizePro já mostra progresso (`AndamentoDoEncaixe`, tentativas/tempo decorrido)
— vale conferir se a apresentação visual está tão clara quanto essa, mas não é uma lacuna
funcional.

## 6. Ajustes de marca/sidebar/rotas (cosmético)

Rotas perderam o `#` (`/encaixe` em vez de `#/encaixe`), sidebar virou uma barra fina de ícones
com sublinhado no item ativo, logo foi do topo pro rodapé da sidebar, cor de destaque agora lida
do próprio arquivo do logo (#ff531f) com contraste checado, tela de Projetos redesenhada
seguindo o "Optmize Lite", e o botão "Levar pro Encaixe" passou a perguntar a unidade antes de
prosseguir. Baixa prioridade — só vale uma olhada rápida se algum dia decidirmos alinhar a
identidade visual do Optimize.App com essa versão nova.

## 7. Sonda dentro de um CorelDRAW via Docker — não se aplica

`corel/OptimizeSonda/AppUI.xslt` é um add-on descartável de diagnóstico, só pra descobrir qual
dos dois navegadores embutidos do CorelDRAW enxerga o documento aberto (explica o item 3).
Conferido: **o OptimizePro não tem nenhuma referência a "corel"/"CorelDRAW" em lugar nenhum do
código** — essa frente inteira não tem contrapartida nem faz sentido ter, já que o OptimizePro
não integra nem automatiza o CorelDRAW.

---

## Próximos passos

Nada daqui foi implementado ainda. Quando o usuário decidir priorizar, o candidato natural é o
**Digitalizar** (itens 1+2) — é a única lacuna de produto real da lista; os demais são
infraestrutura específica do optmize-full (item 3 e 7) ou já resolvidos/cosméticos (itens 4, 5,
6).
