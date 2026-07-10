#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $root ".dotnet\dotnet.exe"
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { "dotnet" }
$appProject = Join-Path $root "src\Helios.Attendance.App\Helios.Attendance.App.csproj"
$appOut = Join-Path $root "publish\app"
$serviceExe = Join-Path $appOut "HeliosAttendanceSync.exe"
$serviceName = "HeliosAttendanceSyncService"
$displayName = "HELIOS Attendance Sync Service"

& $dotnet publish $appProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $appOut

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -ne "Stopped") {
        Stop-Service -Name $serviceName -Force
        $existing.WaitForStatus("Stopped", "00:00:30")
    }

    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}

New-Service -Name $serviceName -BinaryPathName "`"$serviceExe`" --service" -DisplayName $displayName -StartupType Automatic
Start-Service -Name $serviceName

Write-Host "$displayName installed and started from $serviceExe --service."
