#Requires -Version 7
<#
.SYNOPSIS
  Generates every icon asset for Compress for Discord from one programmatic artwork definition.

.DESCRIPTION
  The artwork (rounded indigo tile + white "compress down onto a bar" glyph) is defined as
  geometry below and rendered with System.Drawing — no ImageMagick/Inkscape required.
  Windows-only generator; the OUTPUTS are committed to the repo, so this script only needs
  to be re-run when the artwork changes.

  Outputs:
    assets/icon.svg                                                    (vector source, same geometry)
    assets/icon.ico                                                    (16,24,32,48,64,128,256 PNG entries)
    assets/icons/hicolor/<S>x<S>/apps/io.github.lexikin.CompressForDiscord.png   (S in 16..512)
    packaging/windows/sparse/Assets/Store44x44.png / Store150x150.png  (sparse MSIX visual assets)
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows) { throw 'make-icons.ps1 uses System.Drawing and must run on Windows.' }
Add-Type -AssemblyName System.Drawing

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$appId    = 'io.github.lexikin.CompressForDiscord'

# ---------------------------------------------------------------------------
# Artwork definition — 512x512 canvas.
#   background: rounded rect (16,16 480x480, r=104), vertical gradient #5865F2 -> #3C45A5
#   glyph (white): arrow shaft rect (216,112 80x148)
#                  arrow head triangle (144,260) (368,260) (256,392)
#                  base bar pill (144,416 224x40, r=20)
# Keep the SVG below in sync with New-IconBitmap — same numbers, two renderers.
# ---------------------------------------------------------------------------

$svg = @'
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#5865F2"/>
      <stop offset="1" stop-color="#3C45A5"/>
    </linearGradient>
  </defs>
  <rect x="16" y="16" width="480" height="480" rx="104" fill="url(#bg)"/>
  <rect x="216" y="112" width="80" height="148" fill="#ffffff"/>
  <polygon points="144,260 368,260 256,392" fill="#ffffff"/>
  <rect x="144" y="416" width="224" height="40" rx="20" fill="#ffffff"/>
</svg>
'@

function Add-RoundedRect(
    [System.Drawing.Drawing2D.GraphicsPath]$path,
    [double]$x, [double]$y, [double]$w, [double]$h, [double]$r) {
    $d = $r * 2
    $path.AddArc([float]$x,          [float]$y,          [float]$d, [float]$d, 180, 90)
    $path.AddArc([float]($x+$w-$d),  [float]$y,          [float]$d, [float]$d, 270, 90)
    $path.AddArc([float]($x+$w-$d),  [float]($y+$h-$d),  [float]$d, [float]$d,   0, 90)
    $path.AddArc([float]$x,          [float]($y+$h-$d),  [float]$d, [float]$d,  90, 90)
    $path.CloseFigure()
}

function New-IconBitmap([int]$size) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $s = $size / 512.0
        $g.ScaleTransform($s, $s)

        # Background tile
        $bgPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
        Add-RoundedRect $bgPath 16 16 480 480 104
        $grad = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            [System.Drawing.PointF]::new(0, 16), [System.Drawing.PointF]::new(0, 496),
            [System.Drawing.Color]::FromArgb(255, 0x58, 0x65, 0xF2),
            [System.Drawing.Color]::FromArgb(255, 0x3C, 0x45, 0xA5))
        $g.FillPath($grad, $bgPath)
        $grad.Dispose(); $bgPath.Dispose()

        $white = [System.Drawing.Brushes]::White

        # Arrow shaft
        $g.FillRectangle($white, 216.0, 112.0, 80.0, 148.0)

        # Arrow head
        $g.FillPolygon($white, [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(144, 260),
            [System.Drawing.PointF]::new(368, 260),
            [System.Drawing.PointF]::new(256, 392)))

        # Base bar (pill)
        $barPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
        Add-RoundedRect $barPath 144 416 224 40 20
        $g.FillPath($white, $barPath)
        $barPath.Dispose()
    }
    finally { $g.Dispose() }
    return $bmp
}

function Get-PngBytes([int]$size) {
    $bmp = New-IconBitmap $size
    try {
        $ms = [System.IO.MemoryStream]::new()
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        return $ms.ToArray()
    }
    finally { $bmp.Dispose() }
}

function Save-Png([int]$size, [string]$path) {
    $dir = Split-Path $path -Parent
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    [System.IO.File]::WriteAllBytes($path, (Get-PngBytes $size))
    Write-Host "  wrote $path"
}

function Save-Ico([int[]]$sizes, [string]$path) {
    # ICO container with PNG-compressed entries (supported since Vista; fine on Win10/11).
    $blobs = @()
    foreach ($sz in $sizes) { $blobs += ,@{ Size = $sz; Bytes = Get-PngBytes $sz } }

    $ms = [System.IO.MemoryStream]::new()
    $bw = [System.IO.BinaryWriter]::new($ms)
    $bw.Write([uint16]0)              # reserved
    $bw.Write([uint16]1)              # type: icon
    $bw.Write([uint16]$blobs.Count)
    $offset = 6 + 16 * $blobs.Count
    foreach ($b in $blobs) {
        $dim = if ($b.Size -ge 256) { 0 } else { $b.Size }   # 0 encodes 256
        $bw.Write([byte]$dim); $bw.Write([byte]$dim)
        $bw.Write([byte]0)            # palette colors
        $bw.Write([byte]0)            # reserved
        $bw.Write([uint16]1)          # planes
        $bw.Write([uint16]32)         # bpp
        $bw.Write([uint32]$b.Bytes.Length)
        $bw.Write([uint32]$offset)
        $offset += $b.Bytes.Length
    }
    foreach ($b in $blobs) { $bw.Write([byte[]]$b.Bytes) }
    $bw.Flush()
    [System.IO.File]::WriteAllBytes($path, $ms.ToArray())
    $bw.Dispose(); $ms.Dispose()
    Write-Host "  wrote $path"
}

Write-Host 'Generating icon assets...'

# Vector source
$assetsDir = Join-Path $repoRoot 'assets'
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null
Set-Content -Path (Join-Path $assetsDir 'icon.svg') -Value $svg -NoNewline -Encoding utf8
Write-Host "  wrote $(Join-Path $assetsDir 'icon.svg')"

# Windows .ico
Save-Ico @(16, 24, 32, 48, 64, 128, 256) (Join-Path $assetsDir 'icon.ico')

# Linux hicolor theme PNGs
foreach ($sz in 16, 32, 48, 64, 128, 256, 512) {
    Save-Png $sz (Join-Path $assetsDir "icons\hicolor\${sz}x${sz}\apps\$appId.png")
}

# Sparse MSIX visual assets
$sparseAssets = Join-Path $repoRoot 'packaging\windows\sparse\Assets'
Save-Png 44  (Join-Path $sparseAssets 'Store44x44.png')
Save-Png 150 (Join-Path $sparseAssets 'Store150x150.png')

Write-Host 'Done.'
