<#
.SYNOPSIS
  Builds the self-contained, unpackaged app, stages it to a short path, and zips it.

.DESCRIPTION
  Publishes FileToMarkdown.App as a self-contained win-x64 app (no .NET or Windows
  App SDK install required on the target), then stages the publish output plus the
  bundled Python runtime and Tesseract data into a SHORT directory (default
  C:\f2md-stage\app). The short path matters: the embedded Python tree has very deeply
  nested paths (onnxruntime, numpy, ...) that blow past Windows' 260-char MAX_PATH when
  the Inno Setup / WiX packagers read them from the long bin\...\publish path.

  Produces dist\FileToMarkdownConverter-portable-x64.zip from the staged folder, and
  leaves the staged folder in place for the installer scripts to consume.

  Note: a single-file .exe is not feasible for WinUI + native PDFium/Tesseract + the
  Python bundle. "Portable" means this xcopy-deployable folder; run the .exe inside it.

.EXAMPLE
  pwsh tools\build-portable.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Platform = 'x64',
    [string]$StageDir = (Join-Path $env:SystemDrive 'f2md-stage\app'),
    # Version stamped into the assemblies; the in-app updater compares this
    # against the latest GitHub Release tag.
    [string]$AppVersion = '0.0.0-dev'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$rid = "win-$Platform"
$proj = Join-Path $RepoRoot 'FileToMarkdown.App\FileToMarkdown.App.csproj'

if (-not (Test-Path (Join-Path $RepoRoot 'python\python.exe'))) {
    throw "Bundled Python not found. Run tools\setup-python.ps1 first."
}

Write-Host "=== Publishing self-contained $rid ($Configuration) ===" -ForegroundColor Cyan
dotnet publish $proj -c $Configuration -p:Platform=$Platform -p:RuntimeIdentifier=$rid `
    -p:SelfContained=true -p:WindowsAppSDKSelfContained=true -p:WindowsPackageType=None `
    -p:PublishTrimmed=false -p:Version=$AppVersion
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Locate the publish folder (the one containing the app exe).
$pub = Get-ChildItem -Path (Join-Path $RepoRoot 'FileToMarkdown.App\bin') -Recurse -Directory -Filter 'publish' |
    Where-Object { Test-Path (Join-Path $_.FullName 'FileToMarkdown.App.exe') } |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $pub) { throw "Could not find publish output" }
Write-Host "Publish folder: $pub"

Write-Host "=== Staging to short path: $StageDir ===" -ForegroundColor Cyan
if (Test-Path $StageDir) { Remove-Item $StageDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $StageDir | Out-Null

# robocopy tolerates long source paths and is fast. Exit codes < 8 are success.
$roboArgs = '/E', '/NFL', '/NDL', '/NJH', '/NJS', '/NC', '/NS', '/NP', '/R:1', '/W:1'
robocopy $pub $StageDir @roboArgs | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy publish failed ($LASTEXITCODE)" }
robocopy (Join-Path $RepoRoot 'python')   (Join-Path $StageDir 'python')   @roboArgs | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy python failed ($LASTEXITCODE)" }
robocopy (Join-Path $RepoRoot 'tessdata') (Join-Path $StageDir 'tessdata') @roboArgs | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy tessdata failed ($LASTEXITCODE)" }
$global:LASTEXITCODE = 0

$dist = Join-Path $RepoRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$zip = Join-Path $dist "FileToMarkdownConverter-portable-$Platform.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Host "=== Zipping -> $zip ===" -ForegroundColor Cyan
# Use bsdtar (built into Windows) — far faster than Compress-Archive for large trees.
$tar = Join-Path $env:WINDIR 'System32\tar.exe'
if (Test-Path $tar) {
    & $tar -a -c -f $zip -C $StageDir '*'
    if ($LASTEXITCODE -ne 0) { throw "tar zip failed" }
}
else {
    Compress-Archive -Path (Join-Path $StageDir '*') -DestinationPath $zip
}

$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Done. Portable zip: $zip ($size MB)" -ForegroundColor Green
Write-Host "Staged app (for installers): $StageDir" -ForegroundColor Green
