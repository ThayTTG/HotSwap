# instalar-inicializacao.ps1
#
# Cria um atalho do HotSwap na pasta "Inicializar" do Windows, para que ele
# apareça na aba "Inicializar" do Gerenciador de Tarefas (junto com Firefox,
# Discord, etc.) e já esteja rodando assim que você faz logon.
#
# COMO USAR:
# 1. Coloque o HotSwap.exe em uma pasta definitiva (ex: C:\Programas\HotSwap).
# 2. Clique com o botão direito neste arquivo .ps1 -> "Executar com o PowerShell".
# 3. Informe o caminho completo do HotSwap.exe quando solicitado.
#
# Rodar de novo substitui o atalho antigo (não duplica).

# Quando aberto via "Executar com o PowerShell" no Explorer, o Windows fecha
# a janela assim que o script termina (ou dá erro) — rápido demais para ler
# qualquer mensagem, inclusive erros de política de execução bloqueando o
# script silenciosamente. Para evitar isso, o script se relança sozinho numa
# janela nova com -NoExit, que fica aberta até você fechar de propósito.
if (-not $env:HOTSWAP_REEXEC) {
    $env:HOTSWAP_REEXEC = "1"
    Start-Process powershell.exe -ArgumentList @(
        "-NoExit",
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", "`"$PSCommandPath`""
    )
    exit
}

$ErrorActionPreference = "Stop"

Write-Host "=== Instalar HotSwap na inicialização do Windows ===" -ForegroundColor Cyan
Write-Host ""

$exePath = Read-Host "Cole o caminho completo do HotSwap.exe (ex: C:\Programas\HotSwap\HotSwap.exe)"
$exePath = $exePath.Trim('"').Trim()

if (-not (Test-Path $exePath)) {
    Write-Host ""
    Write-Host "ERRO: não encontrei um arquivo em '$exePath'." -ForegroundColor Red
    Write-Host "Verifique o caminho e rode o script de novo." -ForegroundColor Red
    Read-Host "Pressione Enter para sair"
    exit 1
}

$startupFolder = [Environment]::GetFolderPath("Startup")
$shortcutPath = Join-Path $startupFolder "HotSwap.lnk"
$workingDir = Split-Path $exePath -Parent

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $workingDir
$shortcut.Description = "HotSwap - troca rápida de resolução"
$shortcut.Save()

Write-Host ""
Write-Host "Atalho criado em:" -ForegroundColor Green
Write-Host "  $shortcutPath" -ForegroundColor Green
Write-Host ""

# Remove a Tarefa Agendada antiga, se existir, para não rodar o HotSwap duas
# vezes ao mesmo tempo no próximo logon (o app já se protege contra isso com
# um Mutex, mas não faz sentido manter os dois mecanismos ativos).
$existing = Get-ScheduledTask -TaskName "HotSwap" -ErrorAction SilentlyContinue
if ($existing) {
    Unregister-ScheduledTask -TaskName "HotSwap" -Confirm:$false
    Write-Host "Tarefa Agendada antiga 'HotSwap' removida (agora usando a pasta Inicializar)." -ForegroundColor Yellow
    Write-Host ""
}

$testar = Read-Host "Quer testar agora, abrindo o HotSwap? (s/n)"
if ($testar -eq "s" -or $testar -eq "S") {
    Start-Process $exePath
    Write-Host "HotSwap iniciado — verifique se o ícone apareceu na bandeja." -ForegroundColor Green
}

Write-Host ""
Write-Host "Pronto. No próximo logon, o HotSwap vai aparecer na aba 'Inicializar' do" -ForegroundColor Cyan
Write-Host "Gerenciador de Tarefas e já vai estar rodando na bandeja." -ForegroundColor Cyan
Read-Host "Pressione Enter para fechar"
