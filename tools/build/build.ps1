[CmdletBinding()]
param(
    [string]$Version,
    [switch]$SkipGates
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

if (-not $Version) {
    $Version = (Get-Content (Join-Path $PSScriptRoot "VERSION") -Raw).Trim()
}

# The 12-gate suite (creeping is added once LOOM lands).
$Gates = @(
    "verify", "homes", "story", "canon", "divine", "save",
    "sites", "replay", "harvest", "plague", "migration", "prejudice"
)

# Godot mono editor — override LM_GODOT if it lives elsewhere.
$Godot = if ($env:LM_GODOT) { $env:LM_GODOT } else { "Godot_v4.6.3-stable_mono_win64.exe" }

function Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

Step "Build sim + console (Release)"
dotnet build (Join-Path $RepoRoot "LivingMyth.slnx") -c Release
if ($LASTEXITCODE -ne 0) { throw "sim/console build failed" }

$console = Join-Path $RepoRoot "src\LivingMyth.Console\LivingMyth.Console.csproj"

if ($SkipGates) {
    Write-Host "Skipping gate suite (-SkipGates)." -ForegroundColor Yellow
} else {
    Step "Gate suite ($($Gates.Count) gates, fail-fast)"
    foreach ($g in $Gates) {
        Write-Host "--- gate: $g ---" -ForegroundColor DarkCyan
        dotnet run --project $console -c Release --no-build -- $g
        if ($LASTEXITCODE -ne 0) { throw "GATE FAILED: $g" }
    }
    Write-Host "All $($Gates.Count) gates green." -ForegroundColor Green
}

Step "Stamp version $Version"
& (Join-Path $PSScriptRoot "stamp-version.ps1") -Version $Version -RepoRoot $RepoRoot
if ($LASTEXITCODE -ne 0) { throw "stamp-version failed" }

Step "Build Godot assembly"
dotnet build (Join-Path $RepoRoot "godot\LivingMyth.Godot.csproj") -c ExportRelease
if ($LASTEXITCODE -ne 0) { throw "godot assembly build failed" }

Step "Headless export (Windows Desktop)"
$distWin = Join-Path $RepoRoot "dist\win"
New-Item -ItemType Directory -Force -Path $distWin | Out-Null
$exe = Join-Path $distWin "LivingMyth.exe"
& $Godot --headless --path (Join-Path $RepoRoot "godot") --export-release "Windows Desktop" $exe
if ($LASTEXITCODE -ne 0) { throw "godot export failed (export templates installed?)" }

Step "Verify shipped data"
$config = Join-Path $distWin "data\config.json"
if (-not (Test-Path $exe)) { throw "export produced no exe at $exe" }
if (-not (Test-Path $config)) { throw "data/config.json did NOT ship beside the exe at $config" }
Write-Host "exe + data/config.json present." -ForegroundColor Green

Step "Zip release"
$zip = Join-Path $RepoRoot "dist\LivingMyth-$Version-win64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $distWin "*") -DestinationPath $zip
Write-Host "Packaged: $zip" -ForegroundColor Green

Write-Host "`nBUILD COMPLETE - Living Myth $Version" -ForegroundColor Green
