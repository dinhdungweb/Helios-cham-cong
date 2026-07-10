#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

$serviceName = "HeliosAttendanceSyncService"
$root = Split-Path -Parent $PSScriptRoot
$preferredExe = Join-Path $root "publish\hoffice\HeliosAttendanceSync.exe"
$fallbackExe = Join-Path $root "publish\app\HeliosAttendanceSync.exe"
$serviceExe = if (Test-Path $preferredExe) { $preferredExe } else { $fallbackExe }

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
