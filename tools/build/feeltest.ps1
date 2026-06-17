[CmdletBinding()]
param(
    [string[]]$Systems,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

# The 8 natural-force systems whose live feel the watch-log signs off on.
if (-not $Systems) {
    $Systems = @(
        "war", "harvest", "plague", "migration",
        "prejudice", "divine", "sites", "replay"
    )
}

# Godot mono editor — override LM_GODOT if it lives elsewhere.
$Godot = if ($env:LM_GODOT) { $env:LM_GODOT } else { "Godot_v4.6.3-stable_mono_win64.exe" }
$GodotProj = Join-Path $RepoRoot "godot"
$outRoot = Join-Path $RepoRoot "dist\feeltest"

if (-not $SkipBuild) {
    Write-Host "=== Build Godot assembly ===" -ForegroundColor Cyan
    dotnet build (Join-Path $RepoRoot "godot\LivingMyth.Godot.csproj") -c Debug
    if ($LASTEXITCODE -ne 0) { throw "godot assembly build failed" }
}

New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

foreach ($sys in $Systems) {
    $dir = Join-Path $outRoot $sys
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Write-Host "`n=== feeltest: $sys → $dir ===" -ForegroundColor Cyan
    $env:LM_SHOTS = $dir
    & $Godot --path $GodotProj
    if ($LASTEXITCODE -ne 0) { Write-Warning "viewer exited non-zero for '$sys'" }
}
Remove-Item Env:\LM_SHOTS -ErrorAction SilentlyContinue

Write-Host "`nFeeltest shots under $outRoot" -ForegroundColor Green
Write-Host "Now walk tools/build/FEELTEST_CHECKLIST.md and sign each system off." -ForegroundColor Green
