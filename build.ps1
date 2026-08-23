# Rebuild the Azaroth Core one-click installer (run on Windows with .NET 8 SDK)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$localSdk = "C:\Users\KingL\.gemini\antigravity-ide\scratch\dotnet-sdk\dotnet.exe"
if (Test-Path $localSdk) {
    $dotnetExe = $localSdk
} else {
    $dotnetExe = Get-Command "dotnet" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
    if (-not $dotnetExe) { $dotnetExe = "dotnet" }
}

& $dotnetExe publish (Join-Path $root "src") -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o (Join-Path $root "dist")
Copy-Item (Join-Path $root "src\config.json") (Join-Path $root "dist\config.json") -Force

Write-Host ""
Write-Host "Done. Distributable files:" -ForegroundColor Green
Get-ChildItem (Join-Path $root "dist") | Format-Table Name, Length

