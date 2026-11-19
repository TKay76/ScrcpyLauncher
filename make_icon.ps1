Add-Type -AssemblyName System.Drawing

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$icoPath = Join-Path $dir "app_icon.ico"

$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.Clear([System.Drawing.Color]::Transparent)

# Helper for rounded rectangle path
function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

# Background rounded square with blue-indigo gradient
$bgPath = New-RoundedRectPath 12 12 232 232 48
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point 12, 12),
    (New-Object System.Drawing.Point 244, 244),
    [System.Drawing.Color]::FromArgb(255, 37, 99, 235),
    [System.Drawing.Color]::FromArgb(255, 79, 70, 229)
)
$g.FillPath($bgBrush, $bgPath)

# Monitor frame (back-left)
$monPath = New-RoundedRectPath 42 54 148 104 14
$monFill = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(40, 255, 255, 255))
$monPen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 10)
$g.FillPath($monFill, $monPath)
$g.DrawPath($monPen, $monPath)

# Monitor stand
$standPen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 10)
$standPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$standPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($standPen, 116, 160, 116, 184)
$g.DrawLine($standPen, 82, 186, 150, 186)

# Play triangle inside monitor
$triPts = @(
    (New-Object System.Drawing.PointF 96, 86),
    (New-Object System.Drawing.PointF 96, 126),
    (New-Object System.Drawing.PointF 132, 106)
)
$g.FillPolygon([System.Drawing.Brushes]::White, $triPts)

# Smartphone overlay (front-right)
$phoneBgPath = New-RoundedRectPath 156 96 66 116 14
$phoneBgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 41, 59))
$phonePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 8)
$g.FillPath($phoneBgBrush, $phoneBgPath)
$g.DrawPath($phonePen, $phoneBgPath)

# Phone screen accent dot / bar
$accentBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 74, 222, 128))
$g.FillEllipse($accentBrush, 183, 188, 12, 12)
$screenPath = New-RoundedRectPath 168 112 42 64 5
$screenBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 56, 189, 248))
$g.FillPath($screenBrush, $screenPath)

$g.Dispose()

# Save as 256x256 PNG-in-ICO
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$pngBytes = $ms.ToArray()
$ms.Dispose()

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]1)
$bw.Write([byte]0)
$bw.Write([byte]0)
$bw.Write([byte]0)
$bw.Write([byte]0)
$bw.Write([uint16]1)
$bw.Write([uint16]32)
$bw.Write([uint32]$pngBytes.Length)
$bw.Write([uint32]22)
$bw.Write($pngBytes)
$bw.Close()
$fs.Close()

Write-Host "Icon created: $icoPath ($($pngBytes.Length) bytes)"
