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
# Artwork definition — 512x512 canvas: a hazard-striped mechanical press squishing a
# vertically-compressed Discord blob.
#   background: rounded rect (16,16 480x480, r=104), light gradient #FBFBFE -> #E6E9F5
#   press (near-black #17171A): two ram bars (196,34 44x118) (272,34 44x118),
#                               neck (176,150 160x42), platen frame (44,196 424x118, r=20)
#   platen face: yellow #F6C915 with black diagonal hazard stripes, clipped to (66,218 380x74)
#   squished Discord: ground shadow ellipse; blurple pill face (132,356 248x104, r=52,
#                     gradient #6470F5 -> #4B55C6); two dark eyes.
# Keep the SVG below in sync with New-IconBitmap — same geometry, two renderers.
# ---------------------------------------------------------------------------

$svg = @'
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#FBFBFE"/><stop offset="1" stop-color="#E6E9F5"/>
    </linearGradient>
    <linearGradient id="face" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#6470F5"/><stop offset="1" stop-color="#4B55C6"/>
    </linearGradient>
    <pattern id="hazard" width="68" height="68" patternUnits="userSpaceOnUse" patternTransform="rotate(-45)">
      <rect width="68" height="68" fill="#F6C915"/><rect width="34" height="68" fill="#17171A"/>
    </pattern>
    <clipPath id="platen"><rect x="66" y="218" width="380" height="74" rx="8"/></clipPath>
  </defs>
  <rect x="16" y="16" width="480" height="480" rx="104" fill="url(#bg)"/>
  <g fill="#17171A">
    <rect x="196" y="34" width="44" height="118" rx="14"/>
    <rect x="272" y="34" width="44" height="118" rx="14"/>
    <rect x="176" y="150" width="160" height="42" rx="12"/>
    <rect x="44" y="196" width="424" height="118" rx="20"/>
  </g>
  <g clip-path="url(#platen)"><rect x="66" y="218" width="380" height="74" fill="url(#hazard)"/></g>
  <ellipse cx="256" cy="464" rx="144" ry="24" fill="#5865F2" opacity="0.45"/>
  <rect x="132" y="356" width="248" height="104" rx="52" fill="url(#face)"/>
  <ellipse cx="216" cy="408" rx="19" ry="24" fill="#2B2F63"/>
  <ellipse cx="296" cy="408" rx="19" ry="24" fill="#2B2F63"/>
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

function Add-RoundedRectFill(
    [System.Drawing.Graphics]$g, [System.Drawing.Brush]$brush,
    [double]$x, [double]$y, [double]$w, [double]$h, [double]$r) {
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    Add-RoundedRect $p $x $y $w $h $r
    $g.FillPath($brush, $p)
    $p.Dispose()
}

function New-IconBitmap([int]$size) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $s = $size / 512.0
        $g.ScaleTransform($s, $s)

        $press = [System.Drawing.Color]::FromArgb(255, 0x17, 0x17, 0x1A)
        $pressBrush = [System.Drawing.SolidBrush]::new($press)

        # Background tile
        $bgPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
        Add-RoundedRect $bgPath 16 16 480 480 104
        $bgGrad = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            [System.Drawing.PointF]::new(0, 16), [System.Drawing.PointF]::new(0, 496),
            [System.Drawing.Color]::FromArgb(255, 0xFB, 0xFB, 0xFE),
            [System.Drawing.Color]::FromArgb(255, 0xE6, 0xE9, 0xF5))
        $g.FillPath($bgGrad, $bgPath)
        $bgGrad.Dispose(); $bgPath.Dispose()

        # ---- Mechanical press (near-black) ----
        Add-RoundedRectFill $g $pressBrush 196 34 44 118 14   # ram bar (left)
        Add-RoundedRectFill $g $pressBrush 272 34 44 118 14   # ram bar (right)
        Add-RoundedRectFill $g $pressBrush 176 150 160 42 12  # neck
        Add-RoundedRectFill $g $pressBrush 44 196 424 118 20  # platen frame

        # ---- Hazard stripes on the platen face ----
        $inner = [System.Drawing.Drawing2D.GraphicsPath]::new()
        Add-RoundedRect $inner 66 218 380 74 8
        $g.SetClip($inner)
        $yellow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 0xF6, 0xC9, 0x15))
        $g.FillPath($yellow, $inner)
        $stripePen = [System.Drawing.Pen]::new($press, 34.0)
        for ($sx = -60.0; $sx -lt 500.0; $sx += 68.0) {
            $g.DrawLine($stripePen, [single]$sx, 312.0, [single]($sx + 114.0), 198.0)
        }
        $stripePen.Dispose(); $yellow.Dispose()
        $g.ResetClip(); $inner.Dispose()

        # ---- Squished Discord ----
        $shadow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(115, 0x58, 0x65, 0xF2))
        $g.FillEllipse($shadow, 112.0, 440.0, 288.0, 48.0)   # ground shadow
        $shadow.Dispose()

        $facePath = [System.Drawing.Drawing2D.GraphicsPath]::new()
        Add-RoundedRect $facePath 132 356 248 104 52
        $faceGrad = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            [System.Drawing.PointF]::new(0, 356), [System.Drawing.PointF]::new(0, 460),
            [System.Drawing.Color]::FromArgb(255, 0x64, 0x70, 0xF5),
            [System.Drawing.Color]::FromArgb(255, 0x4B, 0x55, 0xC6))
        $g.FillPath($faceGrad, $facePath)
        $faceGrad.Dispose(); $facePath.Dispose()

        $eye = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 0x2B, 0x2F, 0x63))
        $g.FillEllipse($eye, 197.0, 384.0, 38.0, 48.0)       # left eye
        $g.FillEllipse($eye, 277.0, 384.0, 38.0, 48.0)       # right eye
        $eye.Dispose()

        $pressBrush.Dispose()
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
