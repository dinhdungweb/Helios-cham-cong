$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$preferredApp = Join-Path $root "publish\hoffice\HeliosAttendanceSync.exe"
$fallbackApp = Join-Path $root "publish\app\HeliosAttendanceSync.exe"
$appPath = if (Test-Path $preferredApp) { $preferredApp } else { $fallbackApp }

if (-not (Test-Path $appPath)) {
    throw "Không tìm thấy HeliosAttendanceSync.exe. Hãy publish app trước."
}

$shell = New-Object -ComObject WScript.Shell
$programsDir = [Environment]::GetFolderPath("Programs")
$desktopDir = [Environment]::GetFolderPath("DesktopDirectory")

$startShortcut = Join-Path $programsDir "HOFFICE.lnk"
$desktopShortcut = Join-Path $desktopDir "HOFFICE.lnk"

foreach ($shortcutPath in @($startShortcut, $desktopShortcut)) {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $appPath
    $shortcut.WorkingDirectory = Split-Path -Parent $appPath
    $shortcut.IconLocation = "$appPath,0"
    $shortcut.Description = "HOFFICE Attendance Sync"
    $shortcut.Save()
}

& $appPath --register-app
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Created shortcuts:"
Write-Host " - $startShortcut"
Write-Host " - $desktopShortcut"
