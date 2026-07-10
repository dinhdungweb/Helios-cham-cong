#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

$serviceName = "HeliosAttendanceSyncService"
$root = Split-Path -Parent $PSScriptRoot
$serviceExe = Join-Path $root "publish\app\HeliosAttendanceSync.exe"

if (Test-Path $serviceExe) {
    $process = Start-Process -FilePath $serviceExe -ArgumentList "--uninstall-service" -Wait -PassThru
    exit $process.ExitCode
}

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

if (-not $existing) {
    Write-Host "Service is not installed."
    exit 0
}

if ($existing.Status -ne "Stopped") {
    Stop-Service -Name $serviceName -Force
    $existing.WaitForStatus("Stopped", "00:00:30")
}

sc.exe delete $serviceName | Out-Null
Write-Host "Service removed."
