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

## Colocando na bandeja ao ligar o PC (Tarefa Agendada)

Você pediu para usar o Agendador de Tarefas do Windows em vez de um
atalho na pasta Inicializar. Passo a passo:

1. Abra o **Agendador de Tarefas** (pesquise "Agendador de Tarefas" no
   menu Iniciar, ou `taskschd.msc`).
2. No painel direito, clique em **Criar Tarefa...** (não "Criar Tarefa
   Básica", para termos mais opções).
3. Aba **Geral**:
   - Nome: `HotSwap`
   - Marque **Executar somente quando o usuário estiver conectado**
     (assim ele aparece na sua sessão/bandeja, não numa sessão oculta).
   - Marque **Executar com privilégios mais altos** apenas se você notar
     que a troca de resolução falha sem isso (normalmente não precisa).
4. Aba **Disparadores** → **Novo...**:
   - Iniciar a tarefa: **Ao fazer logon**
   - Usuário específico: seu usuário
   - Clique OK.
5. Aba **Ações** → **Novo...**:
   - Ação: **Iniciar um programa**
   - Programa/script: caminho completo do `HotSwap.exe`
     (ex: `C:\Users\<você>\AppData\Local\HotSwap\HotSwap.exe`)
   - Clique OK.
6. Aba **Condições**:
   - Desmarque "Iniciar a tarefa somente se o computador estiver com
     alimentação de CA" (para notebooks, se relevante).
7. Aba **Configurações**:
   - Desmarque "Parar a tarefa se ela for executada por mais de..." (o
     HotSwap roda o tempo todo na bandeja, então isso mataria o app).
8. Clique **OK** e informe sua senha do Windows se solicitado.

Pronto — no próximo logon, o HotSwap já vai estar lá na bandeja, como
você descreveu, sem parecer que "autoiniciou": ele só já está no lugar
certo.

### Testar sem reiniciar

Clique com o botão direito na tarefa criada → **Executar**. O ícone deve
aparecer na bandeja em segundos.

## Estrutura do projeto

```
HotSwap/
├── HotSwap.csproj          Projeto .NET (WinForms, net8.0-windows)
├── app.manifest             Manifesto (DPI aware, sem prompt de UAC)
├── app.ico                  Ícone do app/bandeja
├── Program.cs                Ponto de entrada (garante instância única)
├── TrayAppContext.cs          Ícone de bandeja, menu de contexto, duplo-clique
├── SaveMainProfileDialog.cs   Diálogo para salvar Perfil 1/2
├── DisplayInterop.cs          P/Invoke: EnumDisplaySettings, ChangeDisplaySettingsEx
├── Models.cs                  Classes do perfis.json
└── ProfileManager.cs           Carrega/salva JSON, detecta modos, aplica perfis
```

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
  ]
}
```

- `mainProfiles`: os 2 perfis principais (o que o duplo-clique alterna).
  Cada um guarda uma configuração por monitor conectado.
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
