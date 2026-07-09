$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$installDir = Join-Path $root ".dotnet"
$installer = Join-Path $env:TEMP "dotnet-install.ps1"

Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installer
powershell -ExecutionPolicy Bypass -File $installer -Channel 8.0 -InstallDir $installDir -Architecture x64 -NoPath

Write-Host ".NET SDK installed to $installDir"
