<#
.SYNOPSIS
  Builds the .exe installer with Inno Setup over the staged publish output.

.DESCRIPTION
  Run tools\build-portable.ps1 first — it produces the self-contained publish folder
  with python/ and tessdata/ staged next to the exe. This script compiles
  installer\setup.iss against that folder, producing dist\FileToMarkdownConverter-Setup.exe.
#>
[CmdletBinding()]
param(
    [string]$AppVersion = '0.1.0',
    [string]$StageDir = (Join-Path $env:SystemDrive 'f2md-stage\app')
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

$pub = $StageDir
if (-not ((Test-Path (Join-Path $pub 'FileToMarkdown.App.exe')) -and
          (Test-Path (Join-Path $pub 'python\python.exe')))) {
    throw "Staged app not found at '$pub'. Run tools\build-portable.ps1 first."
}

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "ISCC.exe (Inno Setup 6) not found. winget install JRSoftware.InnoSetup" }

New-Item -ItemType Directory -Force -Path (Join-Path $RepoRoot 'dist') | Out-Null

Write-Host "=== Compiling Inno Setup installer ===" -ForegroundColor Cyan
& $iscc "/DPublishDir=$pub" "/DAppVersion=$AppVersion" (Join-Path $RepoRoot 'installer\setup.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

$out = Join-Path $RepoRoot 'dist\FileToMarkdownConverter-Setup.exe'
if (Test-Path $out) {
    $size = [math]::Round((Get-Item $out).Length / 1MB, 1)
    Write-Host "Done. EXE installer: $out ($size MB)" -ForegroundColor Green
}
