# desinstalar-inicializacao.ps1
#
# Remove o atalho do HotSwap da pasta "Inicializar" do Windows,
# desfazendo o que instalar-inicializacao.ps1 fez.

$startupFolder = [Environment]::GetFolderPath("Startup")
$shortcutPath = Join-Path $startupFolder "HotSwap.lnk"

if (Test-Path $shortcutPath) {
    Remove-Item $shortcutPath -Force
    Write-Host "Atalho removido de: $shortcutPath" -ForegroundColor Green
} else {
    Write-Host "Nenhum atalho do HotSwap foi encontrado na pasta Inicializar." -ForegroundColor Yellow
}

Read-Host "Pressione Enter para fechar"
