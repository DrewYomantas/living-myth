[CmdletBinding()]
param(
    [string]$Version,
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

if (-not $Version) {
    $Version = (Get-Content (Join-Path $PSScriptRoot "VERSION") -Raw).Trim()
}

$commit = "dev"
try {
    $c = git -C $RepoRoot rev-parse --short HEAD
    if ($LASTEXITCODE -eq 0 -and $c) { $commit = $c.Trim() }
} catch { }

$built = (Get-Date).ToString("yyyy-MM-dd")

# Version.cs — the assembly-visible build stamp (LivingMyth.Godot.BuildInfo).
$versionCs = @"
namespace LivingMyth.Godot;

// Generated/overwritten by tools/build/stamp-version.ps1 at build time. This committed copy
// keeps the project building from a clean checkout (the stamper rewrites it for releases).
public static class BuildInfo
{
    public const string Version = "$Version";
    public const string Commit = "$commit";
    public const string Built = "$built";
}
"@
$versionCsPath = Join-Path $RepoRoot "godot\Version.cs"
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($versionCsPath, ($versionCs -replace "`r`n", "`n"), $utf8)

# export_presets.cfg — file/product version are 4-part (a.b.c.0).
$fourPart = ($Version -split '\.')[0..2] -join '.'
$fourPart = "$fourPart.0"
$presetPath = Join-Path $RepoRoot "godot\export_presets.cfg"
if (Test-Path $presetPath) {
    $preset = Get-Content $presetPath -Raw
    $preset = $preset -replace 'application/file_version="[^"]*"', "application/file_version=`"$fourPart`""
    $preset = $preset -replace 'application/product_version="[^"]*"', "application/product_version=`"$fourPart`""
    [System.IO.File]::WriteAllText($presetPath, ($preset -replace "`r`n", "`n"), $utf8)
}

Write-Host "Stamped version $Version ($commit, $built)"
Write-Host "  godot/Version.cs"
Write-Host "  godot/export_presets.cfg → $fourPart"
