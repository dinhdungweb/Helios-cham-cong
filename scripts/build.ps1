$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $root ".dotnet\dotnet.exe"
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { "dotnet" }

& $dotnet build (Join-Path $root "HeliosAttendanceSync.sln")
