# Rebuild the Azaroth Core one-click installer (run on Windows with .NET 8 SDK)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$distExe = Join-Path $root "dist\setup.exe"
$runningProc = Get-Process -Name "setup" -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $distExe }
if ($runningProc) {
    Write-Host "Warning: dist\setup.exe is currently running. Attempting to stop process (PID $($runningProc.Id))..." -ForegroundColor Yellow
    try {
        Stop-Process -Id $runningProc.Id -Force -ErrorAction Stop
        Start-Sleep -Milliseconds 500
    } catch {
        Write-Host "Could not automatically stop setup.exe. Please close setup.exe manually and re-run build.ps1." -ForegroundColor Red
        exit 1
    }
}

$localSdk = "C:\Users\KingL\.gemini\antigravity-ide\scratch\dotnet-sdk\dotnet.exe"
if (Test-Path $localSdk) {
    $dotnetExe = $localSdk
} else {
    $dotnetExe = Get-Command "dotnet" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
    if (-not $dotnetExe) { $dotnetExe = "dotnet" }
}

Write-Host "Publishing Azaroth Core One-Click Installer (win-x64)..." -ForegroundColor Cyan
& $dotnetExe publish (Join-Path $root "src") -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o (Join-Path $root "dist")

Copy-Item (Join-Path $root "src\config.json") (Join-Path $root "dist\config.json") -Force
if (Test-Path (Join-Path $root "src\AzarothCore-Server-Repack.zip")) {
    Copy-Item (Join-Path $root "src\AzarothCore-Server-Repack.zip") (Join-Path $root "dist\AzarothCore-Server-Repack.zip") -Force
}

Write-Host ""
Write-Host "Done! Distributable output in dist\:" -ForegroundColor Green
Get-ChildItem (Join-Path $root "dist") | Format-Table Name, Length


