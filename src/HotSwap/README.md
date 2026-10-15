# HotSwap

Utilitário de bandeja do sistema para Windows que alterna a resolução e a
taxa de atualização (Hz) dos seus monitores com um duplo clique.

## O que ele faz

- Fica na bandeja do sistema (perto do relógio), sem janela visível.
- **Duplo clique** no ícone: alterna entre **Perfil 1** e **Perfil 2** (os
  dois perfis principais).
- **Clique direito**: abre um menu com os dois perfis principais, mais
  todos os perfis por monitor — incluindo os "Perfis padrão" detectados
  automaticamente (todas as resoluções/Hz que o monitor suporta) e
  quaisquer perfis personalizados que você salvar.
- Na primeira execução, detecta os monitores conectados, lista todas as
  resoluções/Hz suportadas por cada um e grava tudo em `perfis.json`.
  Também salva o estado atual dos monitores como **Perfil 1** e
  **Perfil 2** de partida (você pode sobrescrever depois pelo menu
  "Salvar estado atual como...").
- **Atalho de teclado global** (padrão: `Ctrl+Alt+R`): alterna entre
  Perfil 1 e Perfil 2 de qualquer lugar do Windows, sem precisar clicar
  no ícone. Configurável pelo menu "Atalho de teclado..." — pode ser
  desativado se preferir só o duplo clique.
- O **ícone da bandeja muda de cor/número** conforme o perfil ativo (1
  ou 2), então dá pra saber qual está em uso sem abrir o menu.
- As notificações de sucesso (balões ao trocar/aplicar um perfil) podem
  ser desativadas pelo menu "Notificações de sucesso" — avisos de erro
  continuam aparecendo sempre, já que esses são acionáveis.
- **Trocar o conteúdo entre duas telas** (atalho padrão: `Ctrl+Alt+S`):
  troca TODAS as janelas que estão em uma tela com TODAS as que estão na
  outra — por exemplo, se o navegador está na Tela 1 e a Unity na Tela 2,
  o atalho troca os dois de lugar. Os monitores continuam nas mesmas
  posições físicas (Tela 1 continua sendo Tela 1); só o conteúdo (as
  janelas) muda de tela. Cada janela mantém sua posição relativa e seu
  estado (normal/maximizada/minimizada) na tela nova. Configurável pelo
  menu "Trocar telas..." — com 3 ou mais monitores, esse menu também
  deixa escolher quais duas telas participam da troca.

O arquivo `perfis.json` fica em:

```
%APPDATA%\HotSwap\perfis.json
```

(normalmente `C:\Users\<você>\AppData\Roaming\HotSwap\perfis.json`)

Pelo menu de contexto, "Abrir pasta de perfis..." leva direto lá — dá pra
editar o JSON manualmente se quiser ajustar posição, criar mais perfis
personalizados, etc.

## Requisitos para compilar

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (o
  "SDK", não só o runtime)
- Visual Studio 2022 (opcional, mas facilita) ou apenas o `dotnet` CLI

## Como compilar

### Opção A — Visual Studio

1. Abra `HotSwap.csproj` no Visual Studio.
2. Selecione a configuração **Release**.
3. Build → Build Solution (Ctrl+Shift+B).
4. O executável fica em `bin\Release\net8.0-windows\win-x64\HotSwap.exe`.

### Opção B — linha de comando

Abra um terminal (PowerShell ou cmd) na pasta do projeto e rode:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

O executável único fica em:

```
bin\Release\net8.0-windows\win-x64\publish\HotSwap.exe
```

> Se quiser um `.exe` que roda em qualquer máquina Windows **sem precisar
> ter o .NET instalado**, troque `--self-contained false` por
> `--self-contained true`. O arquivo fica bem maior (~150MB), mas não
> depende de nada.

Depois de compilado, copie a pasta `publish` (ou pelo menos o
`HotSwap.exe`) para onde quiser deixá-lo definitivamente, por exemplo:

```
C:\Users\<você>\AppData\Local\HotSwap\HotSwap.exe
```

## Colocando na bandeja ao ligar o PC

O jeito mais simples e confiável é um atalho na pasta **Inicializar** do
Windows — o mesmo mecanismo usado por Firefox, Discord, Steam, etc., e
por isso o HotSwap aparece listado normalmente na aba "Inicializar" do
Gerenciador de Tarefas (Ctrl+Shift+Esc).

### Opção A — script pronto (recomendado)

Na raiz do projeto há dois scripts PowerShell:

- `instalar-inicializacao.ps1` — pede o caminho do `HotSwap.exe` e cria
  o atalho na pasta Inicializar automaticamente.
- `desinstalar-inicializacao.ps1` — remove esse atalho.

Clique com o botão direito em `instalar-inicializacao.ps1` → **Executar
com o PowerShell**, informe o caminho do `.exe` quando pedido, e pronto.

### Opção B — manualmente

1. Compile ou baixe o `HotSwap.exe` e coloque em uma pasta definitiva
   (ex: `C:\Programas\HotSwap`).
2. Pressione `Win + R`, digite `shell:startup` e aperte Enter — isso
   abre a pasta Inicializar do seu usuário.
3. Crie um atalho do `HotSwap.exe` dentro dessa pasta (botão direito no
   `.exe` → Criar atalho → mova o atalho para a pasta Inicializar).

Pronto — no próximo logon, o HotSwap já vai estar lá na bandeja.

> **Nota sobre Tarefa Agendada:** é possível configurar o HotSwap para
> iniciar via Agendador de Tarefas do Windows em vez da pasta
> Inicializar (mais controle sobre condições de disparo), mas isso faz
> o app **não aparecer** na aba "Inicializar" do Gerenciador de
> Tarefas — essa aba lista apenas os mecanismos clássicos de
> autostart (pasta Inicializar + chaves do Registro `Run`), não
> Tarefas Agendadas. Se você quer ver o HotSwap nessa lista junto com
> seus outros programas, use a pasta Inicializar (Opção A ou B acima).

## Estrutura do projeto

```
HotSwap/
├── HotSwap.csproj               Projeto .NET (WinForms, net8.0-windows)
├── app.manifest                 Manifesto (DPI aware, sem prompt de UAC)
├── app.ico                      Ícone (16–256 px; embutido no .exe)
├── Program.cs                   Ponto de entrada (garante instância única)
├── TrayAppContext.cs            Ícone de bandeja, menu, duplo-clique, atalhos
├── ProfileManager.cs            Carrega/salva JSON, detecta modos, aplica perfis
├── Models.cs                    Classes do perfis.json
├── DisplayInterop.cs            P/Invoke: EnumDisplaySettings, ChangeDisplaySettingsEx
├── WindowSwapper.cs             P/Invoke: EnumWindows, SetWindowPlacement — troca de telas
├── GlobalHotkey.cs              P/Invoke: RegisterHotKey (suporta múltiplos atalhos)
├── Theme.cs                     Paleta, fontes, ajustes de DPI e da janela (DWM)
├── Glyphs.cs                    Ícones de linha do menu, desenhados em código
├── MenuRenderer.cs              Visual do menu da bandeja (e itens customizados)
├── ThemedControls.cs            Botão, switch, campo de atalho, seleção, campo de texto
├── ThemedDialog.cs              Base dos diálogos (cabeçalho, corpo, botões)
├── HotkeySettingsDialog.cs      Diálogo do atalho Perfil 1/2
├── SwapScreensSettingsDialog.cs Diálogo do atalho de trocar telas
└── SaveMainProfileDialog.cs     Diálogo para salvar Perfil 1/2
```

As fontes do ícone ficam em `assets/icon/` na raiz do repositório: um SVG
"mestre" (256 px) e versões redesenhadas pixel a pixel para 16, 20, 24 e
32 px — os tamanhos da bandeja em 100%, 125%, 150% e 200% de escala.

## Formato do `perfis.json`

```json
{
  "version": 1,
  "activeMainProfileIndex": 0,
  "mainProfiles": [
    {
      "name": "Perfil 1",
      "monitorSettings": [
        {
          "id": "a1b2c3d4",
          "name": "Perfil 1",
          "isMain": true,
          "monitorDeviceName": "\\\\.\\DISPLAY1",
          "monitorFriendlyName": "Generic PnP Monitor",
          "width": 1920,
          "height": 1080,
          "hz": 144,
          "posX": 0,
          "posY": 0,
          "orientation": "Landscape"
        }
      ]
    },
    { "name": "Perfil 2", "monitorSettings": [ ... ] }
  ],
  "monitors": [
    {
      "deviceName": "\\\\.\\DISPLAY1",
      "friendlyName": "Generic PnP Monitor",
      "profiles": [
        {
          "name": "1920x1080 @ 144Hz",
          "isAutoDetected": true,
          "width": 1920,
          "height": 1080,
          "hz": 144,
          ...
        },
        { "name": "2560x1440 @ 60Hz", "isAutoDetected": true, ... }
      ]
    }
  ],
  "settings": {
    "notificationsEnabled": true,
    "hotkeyEnabled": true,
    "hotkeyString": "Ctrl+Alt+R",
    "swapHotkeyEnabled": true,
    "swapHotkeyString": "Ctrl+Alt+S",
    "swapMonitorA": "",
    "swapMonitorB": ""
  }
}
```

- `mainProfiles`: os 2 perfis principais (o que o duplo-clique alterna).
  Cada um guarda uma configuração por monitor conectado.
- `settings`: preferências gerais — pode ser editado manualmente também
  (ex: trocar `hotkeyString` para outra combinação, ou desativar o
  atalho/notificações direto no arquivo).
- `monitors[].profiles`: perfis por monitor — os detectados
  automaticamente (`isAutoDetected: true`) e quaisquer outros que você
  adicionar manualmente editando o JSON (`isAutoDetected: false`).

Você pode editar esse arquivo manualmente para criar novos perfis
personalizados por monitor (eles aparecem automaticamente no submenu
"Perfis personalizados" de cada monitor) — só respeite as chaves
existentes. Depois de editar, use "Detectar novamente" no menu ou
reinicie o app para recarregar.

## Notas técnicas

- A troca de resolução usa `ChangeDisplaySettingsEx` do `user32.dll`,
  a mesma API que o próprio Windows usa — não há necessidade de
  privilégios de administrador na maioria dos casos.
- Antes de aplicar qualquer perfil, o HotSwap confere se a combinação de
  resolução+Hz salva realmente existe na lista de modos que o driver do
  monitor reporta como suportados (`EnumDisplaySettings`). É comum um
  monitor aceitar um Hz alto só em certas resoluções — por exemplo,
  75Hz em 800x600 mas não em 1024x768. Se o perfil salvo não bater com
  nenhum modo real, o HotSwap recusa a troca e mostra no balão da
  bandeja quais Hz são realmente suportados naquela resolução, em vez
  de simplesmente falhar sem explicação. Se isso acontecer, edite o
  perfil (pelo menu "Salvar estado atual como..." ou direto no JSON)
  usando um dos Hz listados no aviso.
- Cada perfil também guarda a profundidade de cor (`BitsPerPixel`) que o
  driver reporta para aquele modo específico. Isso importa porque a
  mesma resolução+Hz pode só ser válida em certos bits por pixel — por
  exemplo, um monitor pode listar 1024x768@75Hz oficialmente só em
  16 bits, mesmo com o desktop rodando em 32 bits no momento. Sem fixar
  o bpp certo, o Windows recusa a troca com `DISP_CHANGE_BADMODE` mesmo
  quando a resolução e o Hz são individualmente válidos — é por isso que
  ferramentas como o Painel Nvidia conseguem aplicar um modo que o
  `ChangeDisplaySettingsEx` "cru" recusaria: elas cuidam desse detalhe
  internamente. Perfis detectados automaticamente já guardam o bpp
  correto; se você editar um perfil manualmente no JSON, é mais seguro
  copiar o `BitsPerPixel` de um perfil detectado com a mesma resolução.
- Rodar duas instâncias ao mesmo tempo é bloqueado por um Mutex — só uma
  fica ativa.
- A troca de conteúdo entre telas usa `EnumWindows` para listar as janelas
  de topo visíveis, ignorando janelas sem título, sem "dono" e janelas de
  ferramenta (`WS_EX_TOOLWINDOW`) — o mesmo tipo de filtro que ferramentas
  de Alt+Tab usam para não mexer em popups/diálogos/janelas auxiliares do
  sistema. Cada janela é associada ao monitor mais próximo dela
  (`MonitorFromWindow`) e reposicionada com `SetWindowPlacement`/
  `SetWindowPos`, preservando a posição relativa dentro da tela nova e o
  estado (normal/maximizada/minimizada). Só janelas que não caibam ainda em
  outro monitor de tamanho diferente têm sua posição/tamanho ajustados
  proporcionalmente para não "vazar" da tela.
