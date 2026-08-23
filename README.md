# HotSwap

Utilitário de bandeja do sistema para Windows que troca a resolução e a
taxa de atualização (Hz) dos seus monitores com um duplo clique.

Nasceu de uma necessidade simples: alternar rapidamente entre duas
configurações de tela (por exemplo, "produtividade" e "jogos") sem abrir
as Configurações de Exibição do Windows toda vez.

## Como funciona

- Fica na bandeja do sistema, sem janela visível no dia a dia.
- **Duplo clique** no ícone: alterna entre **Perfil 1** e **Perfil 2**
  (os dois perfis principais).
- **Clique direito**: menu com os dois perfis principais, mais todos os
  perfis por monitor — incluindo os "Perfis padrão" detectados
  automaticamente (todas as resoluções/Hz suportadas pelo monitor).
- Na primeira execução, detecta os monitores conectados e grava as
  resoluções/Hz suportadas em um `perfis.json`, organizado por monitor.
- Pode ser configurado para já estar na bandeja assim que o Windows liga
  (via Tarefa Agendada — instruções abaixo).

## Baixar e usar (sem precisar programar)

1. Vá em [Releases](../../releases) e baixe o `.zip` da versão mais
   recente (`HotSwap-vX.X.X-win-x64.zip`).
2. Extraia para uma pasta permanente no seu PC, por exemplo
   `C:\Programas\HotSwap`.
3. Rode `HotSwap.exe`. Um ícone deve aparecer na bandeja do sistema
   (perto do relógio).
4. Clique com o botão direito no ícone → explore o menu para ver os
   perfis detectados automaticamente para o seu monitor.
5. (Opcional) Configure para iniciar com o Windows via Tarefa Agendada —
   veja o passo a passo em [`src/HotSwap/README.md`](src/HotSwap/README.md#colocando-na-bandeja-ao-ligar-o-pc-tarefa-agendada).

**Requisito:** [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
(escolha "Desktop Runtime x64"). Se você já usa Visual Studio 2022 ou
outros programas .NET, provavelmente já tem isso instalado.

O `perfis.json` gerado fica em `%APPDATA%\HotSwap\perfis.json` — o menu
de contexto tem um atalho "Abrir pasta de perfis..." para chegar lá
rápido, e "Salvar estado atual como..." para redefinir o Perfil 1/2 sem
editar o arquivo manualmente.

## Para desenvolvedores — build local, código e contribuições

O projeto é C# / .NET 8 (WinForms), usando P/Invoke direto sobre as APIs
nativas do Windows (`ChangeDisplaySettingsEx`, `EnumDisplaySettings`,
`EnumDisplayDevices`) para detectar monitores e trocar resolução — sem
dependências externas.

```powershell
git clone https://github.com/<seu-usuario>/<seu-repo>.git
cd <seu-repo>
dotnet build src/HotSwap/HotSwap.csproj
```

Documentação técnica completa (arquitetura, formato do `perfis.json`,
decisões de implementação e por que certas escolhas de P/Invoke foram
feitas) está em [`src/HotSwap/README.md`](src/HotSwap/README.md).

Quer contribuir, propor uma feature, ou usar este código como base para
seu próprio projeto? Sinta-se à vontade — veja
[`CONTRIBUTING.md`](CONTRIBUTING.md) para como abrir issues/PRs. O
projeto é licenciado sob [MIT](LICENSE): pode usar, modificar, estudar e
redistribuir livremente, inclusive em projetos comerciais, mantendo o
aviso de copyright.

### Ideias de segmentação/expansão em aberto

Se você quiser pegar o projeto e evoluir, alguns caminhos naturais que
ainda não foram implementados:

- Suporte a mais de 2 perfis "principais" com atalho de teclado global.
- Interface gráfica de edição de perfis (hoje a edição fina é via JSON).
- Perfis condicionais (ex: aplicar automaticamente ao abrir um jogo
  específico).
- Suporte a migrar/importar o layout de posição de múltiplos monitores
  via a API `SetDisplayConfig`/CCD, mais moderna que
  `ChangeDisplaySettingsEx`.

## Releases automáticas

Toda tag `vX.Y.Z` enviada ao repositório dispara um workflow do GitHub
Actions (`.github/workflows/release.yml`) que compila o projeto e
publica o `.exe` (compactado em `.zip`) automaticamente na página de
Releases — ninguém precisa compilar manualmente para disponibilizar uma
nova versão.
