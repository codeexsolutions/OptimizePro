# Guia de Uso — Optimize

Este é um guia prático de como usar o Optimize no dia a dia. Para detalhes técnicos
(arquitetura, algoritmos, schema do banco), ver `ARQUITETURA-DOTNET-DESKTOP-MVVM.md` e
`ESPECIFICACAO-PARA-DOTNET.md`.

---

## 1. O que é o Optimize

O Optimize ajuda confecções e ateliês a:

- **Cadastrar moldes** (peças de roupa/produto) a partir de arquivos DXF, PLT, SVG ou PDF vetorial.
- **Organizar projetos** por cliente — trabalho que se repete, com suas próprias peças.
- **Encaixar** as peças no tecido buscando o menor consumo de metragem possível.
- **Vetorizar imagens** — transformar uma foto/arte em traço vetorial (SVG) pronto pra corte.
- Ajustar **configurações** de disparo e exportação.

O app roda localmente no Windows — os dados ficam em `%LOCALAPPDATA%\Optimize`.

---

## 2. Abrindo o app

Ao abrir, o Optimize já entra na tela **Moldes**. A navegação fica na barra lateral
esquerda, dividida em dois grupos:

- **PRODUÇÃO**: Moldes, Projetos, Encaixe, Vetor
- **COMUNICAÇÃO**: Disparo, Configurações

Clique em qualquer item pra trocar de tela.

> **Disparo** ainda não está funcional — está em stand-by. A ideia original (envio de
> mensagens em massa pelo WhatsApp) foi trocada por **captação de lead**, enviando os dados
> pra uma base separada (Supabase) via notificação silenciosa. A origem exata do lead ainda
> não foi definida, então o módulo segue sem desenvolvimento por enquanto. A tela existe mas
> é só uma prévia.

---

## 3. Moldes

Tela inicial: lista de moldes já cadastrados, com botão **+ Adicionar molde**.

### Criar um molde

1. Clique em **+ Adicionar molde**.
2. Preencha o **nome do molde** (ex.: "Camiseta básica") e, se quiser, uma observação.
3. Na seção **Peças**, cada peça representa uma parte do molde (frente, costas, manga...).
   Pra cada peça:
   - Escolha o **papel** (frente, costas, manga direita, manga esquerda, manga, gola, punho,
     cós, bolso, vista, forro ou outro).
   - Dê um nome (opcional), tamanho (ex.: "P", "M", ou deixe "único") e a **quantidade** de
     cópias dessa peça.
   - Clique em **Escolher arquivo** e selecione um DXF, PLT, SVG ou PDF vetorial. A largura,
     altura e o contorno da peça vêm automaticamente do arquivo — não precisa digitar medida.
   - Use **+ Adicionar peça** pra incluir mais partes, e **Remover** pra tirar uma peça.
4. Clique em **Salvar molde**.

Uma peça só é salva se tiver um arquivo válido carregado (contorno com pelo menos 3 pontos).
Peças sem arquivo são ignoradas ao salvar.

### Excluir um molde

Na lista de Moldes, clique em **Excluir** ao lado do molde desejado. Isso remove o molde,
suas peças e estampas associadas.

---

## 4. Projetos

Tela de duas colunas: **clientes** à esquerda, **projetos do cliente selecionado** à direita.

### Cadastrar um cliente

Digite o nome no campo "Nome do novo cliente" e clique no botão **+**. O cliente aparece na
lista com a contagem de projetos.

### Criar um projeto

1. Clique num cliente pra selecioná-lo.
2. Digite o nome do novo projeto e clique em **+ Novo projeto** — isso já abre o editor do
   projeto recém-criado.

### Editar um projeto

No editor:

- **Dados do projeto**: nome, observações, largura do tecido, espaço/folga, margem e giro
  (180°, fixa ou livre) — usados depois na hora do encaixe, se o projeto virar um encaixe.
- **Peças**: cada peça do projeto é uma imagem (PNG/JPG) com nome, largura, altura e
  quantidade digitadas à mão (diferente do molde, aqui não se extrai contorno vetorial —
  é só a imagem de referência da peça).
  - Clique em **Escolher imagem** pra anexar a foto/arte de cada peça.
- Clique em **Salvar projeto** ao terminar.

### Excluir cliente ou projeto

Use os botões **✕** (cliente) ou **Excluir** (projeto) nas respectivas listas. Excluir um
cliente remove também todos os projetos dele.

---

## 5. Encaixe

Aqui o Optimize busca o melhor jeito de encaixar as peças de um molde no tecido.

1. Escolha o **molde** no menu suspenso — as peças dele aparecem numa lista, cada uma com
   uma caixa de seleção e a quantidade que entrará no encaixe (desmarque ou zere a
   quantidade das peças que não quer incluir).
2. Configure o **tecido**:
   - **Largura do tecido (cm)**
   - **Espaço/folga (cm)** — distância mínima entre peças
   - **Margem (cm)** — margem nas pontas do rolo
   - **Buscar por (segundos)** — quanto tempo a busca roda tentando melhorar o resultado
3. Clique em **Fazer encaixe**. Enquanto roda, aparece o número de tentativas e o melhor
   consumo encontrado até o momento.
4. Se quiser parar antes do tempo configurado acabar, clique em **Parar e usar este** — o
   Optimize usa o melhor resultado que já tinha achado até ali.
5. Ao terminar, a seção **Resultado** mostra:
   - **Consumo** (metragem de tecido gasta)
   - **Aproveitamento** (% da área do tecido realmente ocupada por peças)
   - **Tentativas** e **peças não encaixadas** (se alguma não coube)
   - Um desenho simplificado (retângulos coloridos) mostrando onde cada peça ficou no rolo

> O motor de busca testa duas estratégias (por contorno e por retângulo) automaticamente e
> fica com a melhor. Agrupamento de peças em blocos (dupla/trio), o motor de faixas e o
> encaixe por NFP ainda não estão implementados — ficam pra uma etapa futura.
>
> Exportar o resultado em PDF/PNG também ainda não está disponível.

---

## 6. Vetor

Transforma uma imagem (foto, desenho) em traço vetorial SVG.

1. Clique em **Escolher imagem** e selecione um arquivo PNG/JPG.
2. A prévia **Original** aparece à esquerda; a prévia **Vetorizado (SVG)** é gerada
   automaticamente à direita alguns instantes depois (o app espera você parar de mexer nos
   parâmetros antes de reprocessar, pra não travar a cada ajuste).
3. Ajuste os parâmetros conforme o resultado:
   - **Cores**: quantas cores a paleta final vai ter (1 = silhueta só com uma cor).
   - **Detalhe**: tamanho mínimo (em pixels) de uma mancha pra não ser tratada como sujeira
     e descartada.
   - **Suavidade**: quanto o traço é simplificado (mais alto = menos pontos, mais suave).
   - **Quina (graus)**: abaixo desse ângulo, um canto vira "vivo" (linha reta); acima, vira
     curva suave.
   - **Tensão**: o quanto as curvas Bézier "estufam".
   - **Juntar sombras**: funde tons de luz/sombra parecidos numa cor só (0 = desligado).
   - **Redondas**: liga/desliga a detecção automática de círculos e elipses.
   - **Subpixel**: liga/desliga o refinamento de borda por anti-aliasing (mais fiel ao
     contorno real da imagem, mas mais lento).
4. Ou use um dos **atalhos prontos** — **Chapada**, **Silhueta**, **Sombra**, **Fino** — que
   ajustam vários parâmetros de uma vez pra um resultado típico.

O SVG final ainda não tem um botão de exportar/salvar em arquivo — por enquanto a
vetorização serve pra pré-visualizar o resultado.

---

## 7. Configurações

Preferências gerais do app:

- **Disparo**: código do país e DDD padrão, mensagem padrão (aceita `{{nome}}` pra
  personalizar) e intervalo mínimo/máximo entre envios. Esses campos ficam guardados pra
  quando o módulo de Disparo (agora repensado como captação de lead — ver seção 8) for
  desenvolvido; hoje não são usados por nenhuma tela ainda.
- **Exportação**: DPI padrão usado quando a exportação de arquivos for implementada.

Clique em **Salvar configurações** pra gravar. As mudanças ficam guardadas no banco local e
valem pra próxima vez que o app abrir.

---

## 8. O que ainda não está pronto

- **Disparo** — em stand-by. Deixou de ser "disparo de mensagens em massa pelo WhatsApp" e
  virou **captação de lead**: receber dados de lead e enviar pra uma base separada
  (Supabase) via notificação/webhook silencioso, sem alertar o usuário do Optimize. Falta
  definir de onde vem o lead (mensagem recebida no WhatsApp? formulário dentro do próprio
  Optimize? outra origem?) antes de começar a construir.
- **Exportação em PDF/PNG** do encaixe e do resultado do Vetor.
- **Agrupamento em blocos** (dupla/trio), motor por **faixas** e encaixe por **NFP** no
  módulo de Encaixe.
- **Memória de aprendizado do Encaixe** — o motor já registra os resultados no banco, mas a
  tela ainda não mostra esse histórico nem usa ele pra sugerir a melhor receita antes de
  buscar. Além disso, o projeto original tem uma peça nova (`encaixe-rede.js`, uma rede
  neural pequena que complementa a memória, generalizando pra trabalhos parecidos — não só
  idênticos) já investigada e documentada (`ESPECIFICACAO-PARA-DOTNET.md` §12.1), mas ainda
  não portada — a memória atual usa só a fórmula de duas camadas (peso geral + peso do
  tipo), que já está confirmada fiel ao original.

Essas partes têm o desenho já pensado na documentação técnica (exceto o novo desenho do
Disparo, ainda em aberto); é questão de continuar o desenvolvimento módulo a módulo, do
mesmo jeito que o restante do app foi construído.
