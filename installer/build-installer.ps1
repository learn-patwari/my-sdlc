# Build SprintForge installer locally
# Run from the repo root: .\installer\build-installer.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

Write-Host "`n==> Publishing SprintForge (self-contained win-x64)..." -ForegroundColor Cyan
dotnet publish "$root\src\SprintForge.Wpf\SprintForge.Wpf.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o "$root\publish\SprintForge-win-x64"

if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed."; exit 1 }

Write-Host "`n==> Running Inno Setup..." -ForegroundColor Cyan
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    Write-Error "Inno Setup 6 not found at '$iscc'. Download from https://jrsoftware.org/isdl.php"
    exit 1
}

& $iscc "$PSScriptRoot\sprintforge.iss"
if ($LASTEXITCODE -ne 0) { Write-Error "Inno Setup failed."; exit 1 }

$output = "$PSScriptRoot\output\Setup.exe"
Write-Host "`n==> Done! Installer at:" -ForegroundColor Green
Write-Host "    $output" -ForegroundColor White
Write-Host ""
