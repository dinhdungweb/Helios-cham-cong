$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $root ".dotnet\dotnet.exe"
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { "dotnet" }
$project = Join-Path $root "src\Helios.Attendance.App\Helios.Attendance.App.csproj"

& $dotnet run --project $project
