[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputs = @(
    (Join-Path $repoRoot 'src\CodexOrbit\app.ico'),
    (Join-Path $repoRoot 'src\CodexOrbit\CodexQuota.Assets.tray-icon.ico')
)
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)

function New-IconFrame {
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
    )
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.CompositingQuality =
            [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $margin = [Math]::Max(1.0, $Size * 0.08)
        $background = [System.Drawing.SolidBrush]::new(
            [System.Drawing.Color]::FromArgb(255, 8, 16, 33)
        )
        try {
            $graphics.FillEllipse(
                $background,
                [single]$margin,
                [single]$margin,
                [single]($Size - 2 * $margin),
                [single]($Size - 2 * $margin)
            )
        }
        finally {
            $background.Dispose()
        }

        $stroke = [Math]::Max(1.5, $Size * 0.085)
        $arcMargin = $margin + $stroke * 1.25
        $arcSize = $Size - 2 * $arcMargin
        $cyan = [System.Drawing.Pen]::new(
            [System.Drawing.Color]::FromArgb(255, 29, 223, 211),
            [single]$stroke
        )
        $violet = [System.Drawing.Pen]::new(
            [System.Drawing.Color]::FromArgb(255, 123, 92, 255),
            [single]$stroke
        )
        try {
            $cyan.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
            $cyan.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
            $violet.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
            $violet.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
            $graphics.DrawArc(
                $cyan,
                [single]$arcMargin,
                [single]$arcMargin,
                [single]$arcSize,
                [single]$arcSize,
                -82,
                225
            )
            $graphics.DrawArc(
                $violet,
                [single]$arcMargin,
                [single]$arcMargin,
                [single]$arcSize,
                [single]$arcSize,
                158,
                102
            )
        }
        finally {
            $cyan.Dispose()
            $violet.Dispose()
        }

        $dotSize = [Math]::Max(1.4, $Size * 0.075)
        $dot = [System.Drawing.SolidBrush]::new(
            [System.Drawing.Color]::FromArgb(255, 231, 249, 255)
        )
        try {
            $graphics.FillEllipse(
                $dot,
                [single](($Size - $dotSize) / 2),
                [single](($Size - $dotSize) / 2),
                [single]$dotSize,
                [single]$dotSize
            )
        }
        finally {
            $dot.Dispose()
        }

        $stream = [System.IO.MemoryStream]::new()
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$frames = foreach ($size in $sizes) {
    [pscustomobject]@{
        Size = $size
        Bytes = New-IconFrame -Size $size
    }
}

foreach ($output in $outputs) {
    $stream = [System.IO.File]::Create($output)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$frames.Count)

        $offset = 6 + (16 * $frames.Count)
        foreach ($frame in $frames) {
            $dimension = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Bytes.Length)
            $writer.Write([uint32]$offset)
            $offset += $frame.Bytes.Length
        }

        foreach ($frame in $frames) {
            $writer.Write($frame.Bytes)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
    Write-Host "已生成：$output"
}
