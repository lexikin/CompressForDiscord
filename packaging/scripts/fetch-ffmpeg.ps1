#Requires -Version 7
<#
.SYNOPSIS
  Downloads the pinned BtbN ffmpeg build for a RID, verifies its sha256 against
  packaging/ffmpeg/ffmpeg.lock.json, and extracts ffmpeg/ffprobe (+ LICENSE) into -OutDir.

.NOTES
  Used by CI (both runners) and for local dev. Skips work when -OutDir already holds
  binaries fetched from the same lock entry (marker file).
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'linux-x64')]
    [string]$Rid = $(if ($IsWindows) { 'win-x64' } else { 'linux-x64' }),

    [Parameter(Mandatory)]
    [string]$OutDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$lockPath = Join-Path $PSScriptRoot '..\ffmpeg\ffmpeg.lock.json'
$lock = Get-Content $lockPath -Raw | ConvertFrom-Json
$asset = $lock.assets.$Rid

if (-not $asset -or $asset.sha256 -match 'TODO') {
    throw "ffmpeg.lock.json has no pinned $Rid asset. Pin a BtbN release (tag + url + sha256) first."
}

$exe = if ($Rid -eq 'win-x64') { '.exe' } else { '' }
$marker = Join-Path $OutDir ".fetched-$($asset.sha256.Substring(0, 16))"

if ((Test-Path $marker) -and
    (Test-Path (Join-Path $OutDir "ffmpeg$exe")) -and
    (Test-Path (Join-Path $OutDir "ffprobe$exe"))) {
    Write-Host "ffmpeg $($lock.ffmpegVersion) ($Rid) already present in $OutDir — skipping."
    return
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$work = Join-Path ([IO.Path]::GetTempPath()) "cfd-ffmpeg-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $work | Out-Null

try {
    $archive = Join-Path $work $asset.fileName
    Write-Host "Downloading $($asset.url)"
    Invoke-WebRequest -Uri $asset.url -OutFile $archive

    $actual = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    $expected = $asset.sha256.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "sha256 mismatch for $($asset.fileName): expected $expected, got $actual. Refusing to continue."
    }

    Write-Host 'Checksum OK. Extracting…'
    $extractDir = Join-Path $work 'extracted'
    New-Item -ItemType Directory -Force -Path $extractDir | Out-Null
    if ($asset.fileName -like '*.zip') {
        Expand-Archive -Path $archive -DestinationPath $extractDir
    }
    else {
        # tar on ubuntu runners (and Windows' bsdtar) handles .tar.xz natively.
        tar -xJf $archive -C $extractDir
        if ($LASTEXITCODE -ne 0) { throw "tar extraction failed with exit code $LASTEXITCODE" }
    }

    foreach ($tool in "ffmpeg$exe", "ffprobe$exe") {
        $found = Get-ChildItem -Path $extractDir -Recurse -File -Filter $tool | Select-Object -First 1
        if (-not $found) { throw "$tool not found inside $($asset.fileName)" }
        Copy-Item $found.FullName (Join-Path $OutDir $tool) -Force
    }

    $license = Get-ChildItem -Path $extractDir -Recurse -File -Filter 'LICENSE*' | Select-Object -First 1
    if ($license) {
        Copy-Item $license.FullName (Join-Path $OutDir 'FFMPEG-LICENSE.txt') -Force
    }

    Get-ChildItem $OutDir -Filter '.fetched-*' | Remove-Item -Force
    New-Item -ItemType File -Path $marker -Force | Out-Null
    Write-Host "ffmpeg $($lock.ffmpegVersion) ($Rid) staged in $OutDir"
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
