# desinstalar-inicializacao.ps1
#
# Remove o atalho do HotSwap da pasta "Inicializar" do Windows,
# desfazendo o que instalar-inicializacao.ps1 fez.

# Ver comentário equivalente em instalar-inicializacao.ps1: evita que a
# janela feche sozinha rápido demais quando aberto via botão direito.
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

$startupFolder = [Environment]::GetFolderPath("Startup")
$shortcutPath = Join-Path $startupFolder "HotSwap.lnk"

if (Test-Path $shortcutPath) {
    Remove-Item $shortcutPath -Force
    Write-Host "Atalho removido de: $shortcutPath" -ForegroundColor Green
} else {
    Write-Host "Nenhum atalho do HotSwap foi encontrado na pasta Inicializar." -ForegroundColor Yellow
}

Read-Host "Pressione Enter para fechar"
