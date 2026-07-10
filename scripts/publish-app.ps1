$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $root ".dotnet\dotnet.exe"
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { "dotnet" }
$appProject = Join-Path $root "src\Helios.Attendance.App\Helios.Attendance.App.csproj"
$appOut = Join-Path $root "publish\app"

& $dotnet publish $appProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $appOut
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "App published to $appOut"
Write-Host "Use HeliosAttendanceSync.exe normally for UI, or with --service for Windows Service mode."
