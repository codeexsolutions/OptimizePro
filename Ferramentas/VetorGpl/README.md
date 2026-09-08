# VetorGpl — motor externo de vetorização (Potrace, GPL-3.0-or-later)

**Este projeto é código GPL-3.0-or-later** (via [BitmapToVector](https://github.com/daltonks/BitmapToVector),
um porte em C# do Potrace). Ele existe como um **executável separado**, de propósito —
nunca deve virar `ProjectReference`/`PackageReference` de `Optimize.App` ou de qualquer
outro projeto fechado desta solução.

## Por que um processo separado

GPL trata como "obra combinada" (que precisa inteira sob GPL) qualquer coisa **linkada**
— chamada de função direta, mesmo em DLL separada, rodando no mesmo processo. Um projeto
separado dentro da mesma solução **não basta** por si só.

O padrão que de fato separa (o mesmo que ferramentas comerciais usam pra chamar programas
GPL como `ffmpeg` sem virar GPL elas mesmas): rodar como um **processo externo de
verdade** — este `.exe`, chamado via `Process.Start` pelo app principal
(`OptimizePro.Services.Vetor.PotraceProcessoService`), comunicação só por arquivo de
entrada + stdout de saída, nunca por chamada de função in-process. Isso é "mera
agregação" (dois programas distribuídos juntos, não uma obra derivada) — este `.exe`
continua GPL (código-fonte precisa ficar disponível, ver `LICENSE-GPL-3.0.txt` nesta
pasta), mas o `Optimize.App` principal permanece fechado.

**Isto não é aconselhamento jurídico definitivo** — confirmar com um advogado antes de
distribuir pra clientes de verdade.

## Uso

```
VetorGpl trace --entrada mascara.png [--turdsize 2] [--alphamax 1.0] [--opttolerance 0.2]
```

- `--entrada`: caminho de uma imagem PNG **preto-e-branco** (preto = forma a traçar,
  branco = fundo) — já preparada pelo chamador (uma camada de cor já quantizada e limpa).
- Saída: um array JSON de strings no stdout, cada uma o atributo `d` de um `<path>` SVG
  (coordenadas em pixels da imagem de entrada). Erro: mensagem no stderr, código de saída
  != 0.
