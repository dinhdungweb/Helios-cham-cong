#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $root ".dotnet\dotnet.exe"
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { "dotnet" }
$appProject = Join-Path $root "src\Helios.Attendance.App\Helios.Attendance.App.csproj"
$appOut = Join-Path $root "publish\app"
$serviceExe = Join-Path $appOut "HeliosAttendanceSync.exe"

& $dotnet publish $appProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $appOut
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$process = Start-Process -FilePath $serviceExe -ArgumentList "--install-service" -Wait -PassThru
if ($process.ExitCode -ne 0) {
    exit $process.ExitCode
}

Write-Host "HELIOS Attendance Sync Service installed and started from $serviceExe --service."
