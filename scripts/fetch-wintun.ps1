<#
.SYNOPSIS
  Fetch the signed Wintun driver into dist-engine/.
.DESCRIPTION
  Wintun is the official WireGuard TUN adapter driver. The binary is not
  stored in the repo; each build downloads it and verifies (warns) on checksum.
.PARAMETER ArchDir
  bin/amd64 | bin/x86
.PARAMETER WintunVersion
  Wintun release version (default 0.14.1).
.PARAMETER ExpectedSha256
  Expected SHA256 of the downloaded zip.
#>
param(
  [Parameter(Mandatory = $true)][string]$ArchDir,
  [string]$WintunVersion = '0.14.1',
  [string]$ExpectedSha256 = '07c256185d6ee3652e09fa55c0b673e2624b565e02c4b9091c79ca7d2f24ef51'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out  = Join-Path $root 'dist-engine'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$url = "https://www.wintun.net/builds/wintun-$WintunVersion.zip"
$zip = Join-Path $env:TEMP 'wintun.zip'

Write-Host "==> Downloading $url"
Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
$sha = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
Write-Host "==> wintun-$WintunVersion.zip sha256=$sha"
if ($ExpectedSha256 -and $sha -ne $ExpectedSha256.ToLower()) {
  Write-Host "::warning::Wintun checksum mismatch. expected=$ExpectedSha256 actual=$sha"
}

$ex = Join-Path (Split-Path $zip) 'wintun-extract'
Remove-Item $ex -Recurse -Force -ErrorAction SilentlyContinue
Expand-Archive $zip -DestinationPath $ex -Force

$dll = Join-Path $ex "wintun/$ArchDir/wintun.dll"
if (-not (Test-Path $dll)) { $dll = Join-Path $ex "$ArchDir/wintun.dll" }
if (-not (Test-Path $dll)) {
  $leaf = Split-Path $ArchDir -Leaf
  $dll = Get-ChildItem $ex -Recurse -Filter 'wintun.dll' |
         Where-Object { $_.FullName -match "\\$leaf\\" } |
         Select-Object -First 1 -ExpandProperty FullName
}
if (-not $dll) { throw "wintun.dll for $ArchDir was not found inside the archive" }

Copy-Item $dll (Join-Path $out 'wintun.dll') -Force
Write-Host "==> wintun.dll ($ArchDir) -> dist-engine/"
