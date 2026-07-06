<#
.SYNOPSIS
  One-time setup of the bundled conversion runtime for File to Markdown Converter.

.DESCRIPTION
  Downloads and assembles the large, regenerable assets that are NOT committed to git:
    * an embedded Python 3.13 runtime with pip + markitdown[all]   -> <repo>\python\
    * ffmpeg (for markitdown audio transcription)                  -> <repo>\python\ffmpeg.exe
    * Tesseract English language data                              -> <repo>\tessdata\eng.traineddata

  markitdown does NOT install on Python 3.14 yet (its dependency magika needs onnxruntime,
  which has no 3.14 wheels), so we bundle our own private Python 3.13. No system Python is used.

  The script is idempotent: each step checks first and skips if already present. Use -Force to redo.

.EXAMPLE
  pwsh tools\setup-python.ps1
#>
[CmdletBinding()]
param(
    [string]$PyVersion,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'   # faster Invoke-WebRequest

# Pinned tool versions (kept fresh by the weekly tool-updates workflow) make
# release builds reproducible instead of installing whatever is latest.
$Versions = Get-Content (Join-Path $PSScriptRoot 'versions.json') -Raw | ConvertFrom-Json
if (-not $PyVersion) { $PyVersion = $Versions.python }

# ---- Paths -----------------------------------------------------------------
$RepoRoot   = Split-Path -Parent $PSScriptRoot
$PythonDir  = Join-Path $RepoRoot 'python'
$TessDir    = Join-Path $RepoRoot 'tessdata'
$TempDir    = Join-Path $env:TEMP 'f2md-setup'
New-Item -ItemType Directory -Force -Path $TempDir | Out-Null

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Get-File($url, $dest) {
    Write-Host "  downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing
}

# ---- 1. Embedded Python 3.13 ----------------------------------------------
Write-Step "Embedded Python $PyVersion"
$pythonExe = Join-Path $PythonDir 'python.exe'
if ($Force -and (Test-Path $PythonDir)) { Remove-Item -Recurse -Force $PythonDir }
if (Test-Path $pythonExe) {
    Write-Host "  [skip] python already present at $PythonDir"
} else {
    New-Item -ItemType Directory -Force -Path $PythonDir | Out-Null
    $zip = Join-Path $TempDir "python-embed.zip"
    Get-File "https://www.python.org/ftp/python/$PyVersion/python-$PyVersion-embed-amd64.zip" $zip
    Expand-Archive -Path $zip -DestinationPath $PythonDir -Force
    Remove-Item $zip -Force

    # Enable site-packages so pip-installed packages are importable.
    $pth = Get-ChildItem -Path $PythonDir -Filter 'python*._pth' | Select-Object -First 1
    if ($pth) {
        $content = Get-Content $pth.FullName
        $content = $content -replace '^\s*#\s*import\s+site', 'import site'
        if ($content -notmatch '(?m)^import\s+site') { $content += 'import site' }
        Set-Content -Path $pth.FullName -Value $content -Encoding ascii
        Write-Host "  enabled 'import site' in $($pth.Name)"
    }
}

# ---- 2. pip ----------------------------------------------------------------
Write-Step "pip"
& $pythonExe -m pip --version
$pipPresent = ($LASTEXITCODE -eq 0)
if ($pipPresent -and -not $Force) {
    Write-Host "  [skip] pip already available"
} else {
    $getpip = Join-Path $TempDir 'get-pip.py'
    Get-File 'https://bootstrap.pypa.io/get-pip.py' $getpip
    & $pythonExe $getpip --no-warn-script-location
    if ($LASTEXITCODE -ne 0) { throw "get-pip.py failed" }
}

# ---- 3. markitdown[all] ----------------------------------------------------
Write-Step "markitdown[all]"
& $pythonExe -c "import markitdown"
$mdPresent = ($LASTEXITCODE -eq 0)
if ($mdPresent -and -not $Force) {
    Write-Host "  [skip] markitdown already importable"
} else {
    & $pythonExe -m pip install --upgrade pip
    & $pythonExe -m pip install "markitdown[all]==$($Versions.markitdown)"
    if ($LASTEXITCODE -ne 0) { throw "markitdown install failed" }
    & $pythonExe -c "from markitdown import MarkItDown; print('markitdown OK')"
    if ($LASTEXITCODE -ne 0) { throw "markitdown import check failed" }
}

# ---- 4. ffmpeg (for audio) -------------------------------------------------
Write-Step "ffmpeg"
$ffmpegExe = Join-Path $PythonDir 'ffmpeg.exe'
if ((Test-Path $ffmpegExe) -and -not $Force) {
    Write-Host "  [skip] ffmpeg already present"
} else {
    try {
        $ffzip = Join-Path $TempDir 'ffmpeg.zip'
        Get-File $Versions.ffmpeg_url $ffzip
        $ffx = Join-Path $TempDir 'ffmpeg-extract'
        if (Test-Path $ffx) { Remove-Item -Recurse -Force $ffx }
        Expand-Archive -Path $ffzip -DestinationPath $ffx -Force
        $found = Get-ChildItem -Path $ffx -Recurse -Filter 'ffmpeg.exe' | Select-Object -First 1
        if ($found) { Copy-Item $found.FullName $ffmpegExe -Force; Write-Host "  ffmpeg -> $ffmpegExe" }
        Remove-Item $ffzip -Force; Remove-Item -Recurse -Force $ffx
    } catch {
        Write-Warning "  ffmpeg download failed ($_). Audio transcription will be unavailable until ffmpeg.exe is placed in $PythonDir."
    }
}

# ---- 5. Tesseract language data -------------------------------------------
Write-Step "Tesseract tessdata (eng)"
New-Item -ItemType Directory -Force -Path $TessDir | Out-Null
$engData = Join-Path $TessDir 'eng.traineddata'
if ((Test-Path $engData) -and -not $Force) {
    Write-Host "  [skip] eng.traineddata already present"
} else {
    Get-File $Versions.tessdata_eng_url $engData
    Write-Host "  eng.traineddata -> $engData"
}

Write-Step "Done"
Write-Host "Python : $PythonDir"
Write-Host "Tess   : $TessDir"
Write-Host "ffmpeg : $ffmpegExe"
