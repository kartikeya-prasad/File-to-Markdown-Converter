<#
.SYNOPSIS
  Builds the .msi installer with the WiX Toolset over the staged publish output.

.DESCRIPTION
  Run tools\build-portable.ps1 first — it produces the self-contained publish folder
  with python/ and tessdata/ staged next to the exe. This script runs `wix build`
  on installer\Product.wxs against that folder, producing
  dist\FileToMarkdownConverter.msi.

  Requires the WiX CLI:  dotnet tool install --global wix
#>
[CmdletBinding()]
param(
    [string]$Platform = 'x64',
    [string]$StageDir = (Join-Path $env:SystemDrive 'f2md-stage\app')
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    throw "wix CLI not found. Run: dotnet tool install --global wix --version 5.0.2"
}

$pub = $StageDir
if (-not ((Test-Path (Join-Path $pub 'FileToMarkdown.App.exe')) -and
          (Test-Path (Join-Path $pub 'python\python.exe')))) {
    throw "Staged app not found at '$pub'. Run tools\build-portable.ps1 first."
}

$dist = Join-Path $RepoRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$out = Join-Path $dist 'FileToMarkdownConverter.msi'

Write-Host "=== Building MSI with WiX ===" -ForegroundColor Cyan
& wix build (Join-Path $RepoRoot 'installer\Product.wxs') -arch $Platform -d "PublishDir=$pub" -o $out
if ($LASTEXITCODE -ne 0) { throw "wix build failed" }

if (Test-Path $out) {
    $size = [math]::Round((Get-Item $out).Length / 1MB, 1)
    Write-Host "Done. MSI installer: $out ($size MB)" -ForegroundColor Green
}
