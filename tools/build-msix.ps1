<#
.SYNOPSIS
  Packs the staged app into a signed MSIX (dist\FileToMarkdownConverter.msix).

.DESCRIPTION
  Consumes the short-path staging folder produced by tools\build-portable.ps1,
  injects packaging\msix\AppxManifest.xml (version-patched) and the MSIX visual
  assets, packs with makeappx, and signs:

    * If SIGNING_PFX_B64 / SIGNING_PFX_PASSWORD are set (GitHub secrets with a
      real code-signing certificate), those are used - no user-side trust step.
    * Otherwise a self-signed certificate is generated for this build; its
      public part is exported to dist\FileToMarkdownConverter.cer so users can
      do the one-time import into "Trusted People" before installing the MSIX.

  Run AFTER build-exe-installer.ps1 / build-msi.ps1: this step adds
  AppxManifest.xml into the shared staging folder, which the other packagers
  must not pick up.

.EXAMPLE
  pwsh tools\build-msix.ps1 -AppVersion 0.3.0
#>
[CmdletBinding()]
param(
    [string]$AppVersion = '0.1.0',
    [string]$StageDir = (Join-Path $env:SystemDrive 'f2md-stage\app')
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $RepoRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

if (-not (Test-Path (Join-Path $StageDir 'FileToMarkdown.App.exe'))) {
    throw "Staged app not found at $StageDir. Run tools\build-portable.ps1 first."
}

# ---- Manifest (4-part numeric version, prerelease suffix dropped) ----------
$numeric = ($AppVersion -split '-')[0]
$parts = @($numeric.Split('.')); while ($parts.Count -lt 3) { $parts += '0' }
$msixVersion = "$($parts[0]).$($parts[1]).$($parts[2]).0"

$manifest = Get-Content (Join-Path $RepoRoot 'packaging\msix\AppxManifest.xml') -Raw
Set-Content -Path (Join-Path $StageDir 'AppxManifest.xml') `
            -Value ($manifest.Replace('{{VERSION}}', $msixVersion)) -Encoding utf8

$assetsDir = Join-Path $StageDir 'Assets'
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null
Copy-Item (Join-Path $RepoRoot 'packaging\msix\Assets\*') $assetsDir -Force

# ---- Windows Kits tools -----------------------------------------------------
function Find-KitTool([string]$name) {
    $kits = 'C:\Program Files (x86)\Windows Kits\10\bin'
    $tool = Get-ChildItem -Path $kits -Recurse -Filter $name -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like '*\x64\*' } |
        Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $tool) { throw "$name not found under $kits" }
    $tool.FullName
}
$makeappx = Find-KitTool 'makeappx.exe'
$signtool = Find-KitTool 'signtool.exe'

# ---- Pack -------------------------------------------------------------------
$msix = Join-Path $dist 'FileToMarkdownConverter.msix'
if (Test-Path $msix) { Remove-Item $msix -Force }
Write-Host "=== makeappx pack ($msixVersion) ===" -ForegroundColor Cyan
& $makeappx pack /d $StageDir /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed ($LASTEXITCODE)" }

# ---- Sign -------------------------------------------------------------------
if ($env:SIGNING_PFX_B64) {
    Write-Host "=== Signing with provided certificate (secrets) ===" -ForegroundColor Cyan
    $pfx = Join-Path $env:TEMP 'f2md-signing.pfx'
    [IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($env:SIGNING_PFX_B64))
    try {
        & $signtool sign /fd SHA256 /f $pfx /p $env:SIGNING_PFX_PASSWORD $msix
        if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE)" }
    }
    finally { Remove-Item $pfx -Force -ErrorAction SilentlyContinue }
}
else {
    Write-Host "=== Signing with build-generated self-signed certificate ===" -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate -Type CodeSigningCert `
        -Subject 'CN=Kartikeya Prasad' `
        -FriendlyName 'File to Markdown Converter (self-signed)' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -NotAfter (Get-Date).AddYears(5)
    $cer = Join-Path $dist 'FileToMarkdownConverter.cer'
    Export-Certificate -Cert $cert -FilePath $cer | Out-Null
    & $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $msix
    if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE)" }
    Write-Host "Public certificate for user trust: $cer" -ForegroundColor Green
}

$size = [math]::Round((Get-Item $msix).Length / 1MB, 1)
Write-Host "Done. MSIX: $msix ($size MB)" -ForegroundColor Green
