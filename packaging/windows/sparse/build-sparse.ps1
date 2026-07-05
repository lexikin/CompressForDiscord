#Requires -Version 7
<#
.SYNOPSIS
  Builds and signs the Win11 sparse MSIX (modern context menu) and signs the shell DLL.

.DESCRIPTION
  Stamps AppxManifest.template.xml (__VERSION__, __CLSID__, extension list from
  packaging/windows/extensions.json), packs with makeappx /nv, then signs BOTH the shell DLL
  and the .msix (some AV configs verify the DLL too — NppShell does the same).

.EXAMPLE
  ./build-sparse.ps1 -Version 0.1.0.0 -ShellDll ..\..\..\src\CompressForDiscord.Shell\x64\Release\CompressForDiscord.Shell.dll `
      -Pfx signing.pfx -PfxPassword secret -Out artifacts\CompressForDiscord.msix
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,      # x.y.z.0
    [Parameter(Mandatory)][string]$ShellDll,
    [Parameter(Mandatory)][string]$Out,
    [string]$Pfx,
    [string]$PfxPassword,
    [switch]$SkipSigning                          # local smoke builds only — unsigned won't register
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Must match ExplorerCommand.h __declspec(uuid(...)).
$Clsid = 'C3E5A2F8-9B1D-4E7A-8F26-D4A0C7B3915E'

function Find-KitTool([string]$name) {
    $kits = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.*\x64\$name" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $kits) { throw "$name not found — install the Windows 10/11 SDK." }
    return $kits.FullName
}

$makeappx = Find-KitTool 'makeappx.exe'
$signtool = Find-KitTool 'signtool.exe'

# ---- stamp the manifest ----
$extensions = (Get-Content (Join-Path $PSScriptRoot '..\extensions.json') -Raw | ConvertFrom-Json).extensions
$items = ($extensions | ForEach-Object {
    "            <desktop5:ItemType Type=`".$_`">`n" +
    "              <desktop5:Verb Id=`"CompressForDiscord`" Clsid=`"$Clsid`" />`n" +
    "            </desktop5:ItemType>"
}) -join "`n"

$manifest = Get-Content (Join-Path $PSScriptRoot 'AppxManifest.template.xml') -Raw
$manifest = $manifest.Replace('__VERSION__', $Version).Replace('__CLSID__', $Clsid)
$manifest = $manifest.Replace('<!--EXTENSION_ITEMS-->', $items)

# ---- stage + pack (sparse: manifest + logo assets only; DLL/exe live in the external location) ----
$stage = Join-Path ([IO.Path]::GetTempPath()) "cfd-sparse-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'Assets') | Out-Null
try {
    Set-Content -Path (Join-Path $stage 'AppxManifest.xml') -Value $manifest -Encoding utf8
    Copy-Item (Join-Path $PSScriptRoot 'Assets\*') (Join-Path $stage 'Assets') -Force

    New-Item -ItemType Directory -Force -Path (Split-Path $Out -Parent) | Out-Null
    & $makeappx pack /d $stage /p $Out /nv /o
    if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit code $LASTEXITCODE" }

    if ($SkipSigning) {
        Write-Warning 'Skipping signing — this package will NOT register outside developer mode.'
        return
    }

    if (-not $Pfx) { throw 'Provide -Pfx/-PfxPassword or -SkipSigning.' }

    foreach ($target in @($ShellDll, $Out)) {
        & $signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /f $Pfx /p $PfxPassword $target
        if ($LASTEXITCODE -ne 0) { throw "signtool failed on $target with exit code $LASTEXITCODE" }
    }

    Write-Host "Signed sparse package: $Out"
}
finally {
    Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
}
