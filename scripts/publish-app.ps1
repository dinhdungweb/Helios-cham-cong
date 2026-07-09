$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $root ".dotnet\dotnet.exe"
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { "dotnet" }
$appProject = Join-Path $root "src\Helios.Attendance.App\Helios.Attendance.App.csproj"
$serviceProject = Join-Path $root "src\Helios.Attendance.Service\Helios.Attendance.Service.csproj"
$appOut = Join-Path $root "publish\app"
$serviceOut = Join-Path $root "publish\service"

& $dotnet publish $appProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $appOut
& $dotnet publish $serviceProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $serviceOut

Write-Host "App published to $appOut"
Write-Host "Service published to $serviceOut"
