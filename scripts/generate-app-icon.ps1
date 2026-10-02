param(
    [string]$OutputPath = "src\Win7BookManagement\Resources\BookDesk.ico"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function New-RoundedPath {
    param(
        [System.Drawing.RectangleF]$Rect,
        [float]$Radius
    )

    $diameter = $Radius * 2.0
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($Rect.Left, $Rect.Top, $diameter, $diameter, 180, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Top, $diameter, $diameter, 270, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rect.Left, $Rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-BookDeskPngBytes {
    param([int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $scale = $Size / 1024.0

        function S([double]$value) {
            return [single]($value * $scale)
        }

        $blue = [System.Drawing.Color]::FromArgb(255, 22, 119, 255)
        $blueInner = [System.Drawing.Color]::FromArgb(255, 36, 132, 255)
        $white = [System.Drawing.Color]::White
        $spine = [System.Drawing.Color]::FromArgb(255, 235, 244, 255)
        $line = [System.Drawing.Color]::FromArgb(255, 168, 204, 255)
        $gold = [System.Drawing.Color]::FromArgb(255, 255, 190, 55)
        $shelf = [System.Drawing.Color]::FromArgb(235, 255, 255, 255)

        $outerRect = New-Object System.Drawing.RectangleF((S 72), (S 72), (S 880), (S 880))
        $outerPath = New-RoundedPath $outerRect (S 190)
        $outerBrush = New-Object System.Drawing.SolidBrush($blue)
        try { $graphics.FillPath($outerBrush, $outerPath) }
        finally { $outerBrush.Dispose(); $outerPath.Dispose() }

        $innerRect = New-Object System.Drawing.RectangleF((S 108), (S 108), (S 808), (S 808))
        $innerPath = New-RoundedPath $innerRect (S 160)
        $innerBrush = New-Object System.Drawing.SolidBrush($blueInner)
        try { $graphics.FillPath($innerBrush, $innerPath) }
        finally { $innerBrush.Dispose(); $innerPath.Dispose() }

        $leftPage = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF((S 210), (S 300))),
            (New-Object System.Drawing.PointF((S 470), (S 350))),
            (New-Object System.Drawing.PointF((S 470), (S 730))),
            (New-Object System.Drawing.PointF((S 230), (S 685))),
            (New-Object System.Drawing.PointF((S 180), (S 625))),
            (New-Object System.Drawing.PointF((S 180), (S 325)))
        )
        $rightPage = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF((S 554), (S 350))),
            (New-Object System.Drawing.PointF((S 814), (S 300))),
            (New-Object System.Drawing.PointF((S 844), (S 325))),
            (New-Object System.Drawing.PointF((S 844), (S 625))),
            (New-Object System.Drawing.PointF((S 794), (S 685))),
            (New-Object System.Drawing.PointF((S 554), (S 730)))
        )

        $whiteBrush = New-Object System.Drawing.SolidBrush($white)
        try {
            $graphics.FillPolygon($whiteBrush, $leftPage)
            $graphics.FillPolygon($whiteBrush, $rightPage)
        }
        finally { $whiteBrush.Dispose() }

        $spineRect = New-Object System.Drawing.RectangleF((S 486), (S 330), (S 52), (S 430))
        $spinePath = New-RoundedPath $spineRect (S 24)
        $spineBrush = New-Object System.Drawing.SolidBrush($spine)
        try { $graphics.FillPath($spineBrush, $spinePath) }
        finally { $spineBrush.Dispose(); $spinePath.Dispose() }

        $lineBrush = New-Object System.Drawing.SolidBrush($line)
        try {
            foreach ($y in @(410, 475, 540, 605)) {
                foreach ($x in @(245, 594)) {
                    $w = if ($x -eq 245) { 185 } else { 185 }
                    $rect = New-Object System.Drawing.RectangleF((S $x), (S $y), (S $w), (S 18))
                    $path = New-RoundedPath $rect (S 9)
                    try { $graphics.FillPath($lineBrush, $path) }
                    finally { $path.Dispose() }
                }
            }
        }
        finally { $lineBrush.Dispose() }

        $bookmark = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF((S 690), (S 250))),
            (New-Object System.Drawing.PointF((S 770), (S 250))),
            (New-Object System.Drawing.PointF((S 770), (S 410))),
            (New-Object System.Drawing.PointF((S 730), (S 378))),
            (New-Object System.Drawing.PointF((S 690), (S 410)))
        )
        $goldBrush = New-Object System.Drawing.SolidBrush($gold)
        try { $graphics.FillPolygon($goldBrush, $bookmark) }
        finally { $goldBrush.Dispose() }

        $shelfRect = New-Object System.Drawing.RectangleF((S 250), (S 782), (S 524), (S 42))
        $shelfPath = New-RoundedPath $shelfRect (S 21)
        $shelfBrush = New-Object System.Drawing.SolidBrush($shelf)
        try { $graphics.FillPath($shelfBrush, $shelfPath) }
        finally { $shelfBrush.Dispose(); $shelfPath.Dispose() }

        $stream = New-Object System.IO.MemoryStream
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            return $stream.ToArray()
        }
        finally { $stream.Dispose() }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $images += ,([PSCustomObject]@{
        Size = $size
        Bytes = (New-BookDeskPngBytes -Size $size)
    })
}

$target = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
} else {
    Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path $OutputPath
}
$directory = Split-Path -Parent $target
New-Item -ItemType Directory -Force -Path $directory | Out-Null

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($stream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$images.Count)

    $offset = 6 + ($images.Count * 16)
    foreach ($image in $images) {
        $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$image.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $image.Bytes.Length
    }

    foreach ($image in $images) {
        $writer.Write($image.Bytes)
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($target, $stream.ToArray())
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

Write-Host "BOOK DESK application icon generated: $target"
