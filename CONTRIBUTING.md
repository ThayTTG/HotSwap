# Contribuindo com o HotSwap

Obrigada por considerar contribuir! Este é um projeto pequeno e pessoal
que virou open source, então o processo é simples e informal.

## Reportando bugs

Abra uma [issue](../../issues/new) descrevendo:

- O que você esperava que acontecesse.
- O que aconteceu de fato.
- Se possível, o resultado do **"Diagnóstico de monitores..."** (menu de
  contexto do ícone da bandeja) — ele mostra exatamente o que o Windows
  reporta sobre seus monitores e ajuda muito a diagnosticar problemas de
  detecção.
- Sua placa de vídeo e se o monitor aparece como "Generic PnP Monitor"
  ou com o nome/modelo real no Gerenciador de Dispositivos (monitores
  sem EDID legível já causaram alguns bugs interessantes neste projeto —
  veja o histórico de commits/issues fechadas para contexto).

## Sugerindo features

Abra uma issue descrevendo o que você quer e por quê. Não tem processo
formal de aprovação — se fizer sentido para o escopo do projeto (um
utilitário simples de troca de resolução), é bem-vindo.

## Enviando código (Pull Requests)

1. Faça um fork do repositório.
2. Crie uma branch a partir da `main`: `git checkout -b minha-feature`.
3. Faça suas mudanças em `src/HotSwap/`.
4. Teste localmente (`dotnet build`, e rode o `.exe` de verdade — muita
   coisa aqui só se manifesta com hardware real, não em teoria).
5. Abra um Pull Request descrevendo o que mudou e por quê.

### Notas técnicas úteis antes de mexer em `DisplayInterop.cs`

Esse arquivo já passou por várias correções não óbvias de P/Invoke.
Antes de alterar as structs `DEVMODE`/`DISPLAY_DEVICE` ou as chamadas a
`ChangeDisplaySettingsEx`, vale saber:

- `CharSet` declarado em um `[DllImport]` **não** se propaga
  automaticamente para o `[StructLayout]` de uma struct passada como
  parâmetro `ref` — os dois precisam declarar o mesmo `CharSet`
  explicitamente, ou os campos de string (`ByValTStr`) ficam com o
  tamanho errado e corrompem o resto da struct.
- Aplicar resolução em **um único monitor** funciona melhor com uma
  chamada direta (`ApplyModeDirect`, sem `CDS_NORESET`). Em monitores
  sem EDID válido ("Generic PnP Monitor"), o padrão de duas etapas
  (`CDS_NORESET` + commit separado) já foi visto retornando sucesso da
  API sem a mudança realmente chegar a acontecer na tela física.
- A mesma resolução+Hz pode ser válida em mais de uma profundidade de
  cor (`dmBitsPerPel`) — aplicar com o bpp errado para aquele modo
  específico causa `DISP_CHANGE_BADMODE` mesmo com resolução/Hz
  corretos. `GetSupportedModes` já cuida de capturar o bpp certo por
  modo.

Se algo parecer "devia funcionar mas não funciona", é bem provável que
seja uma dessas pegadinhas de nível API do Windows, não um erro óbvio de
lógica — o menu "Diagnóstico de monitores..." existe justamente para
investigar esse tipo de caso.

### Identidade visual

- Cores, fontes e helpers de DPI ficam todos em `Theme.cs` — use
  `Theme.Accent`, `Theme.Surface` etc. em vez de cores soltas no código.
- O menu da bandeja é desenhado por `MenuRenderer.cs`. Submenus não herdam
  o renderer do menu pai: depois de montar os itens, chame
  `Theme.StyleDropDown(menu)` (ele desce a árvore sozinho).
- Diálogos novos devem herdar de `ThemedDialog` e usar os controles de
  `ThemedControls.cs` (os controles padrão do WinForms não respeitam bem o
  tema escuro). Escreva medidas em pixels de 96 DPI — o escalonamento é
  automático.
- Ícones de menu são desenhados em código (`Glyphs.cs`), num grid de
  16×16, para ficarem nítidos em qualquer escala.
- O ícone do app vem de `assets/icon/`. Se mudar o desenho, regenere o
  `src/HotSwap/app.ico` com todos os tamanhos (16, 20, 24, 32, 40, 48, 64,
  128, 256) — os de 16 a 32 px são desenhos próprios, não reduções do de
  256.

## Código de conduta

Seja gentil. É um projeto pequeno feito por diversão e utilidade — trate
os outros colaboradores como gostaria de ser tratada.
