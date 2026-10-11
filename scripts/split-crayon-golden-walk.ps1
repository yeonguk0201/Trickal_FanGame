param(
    [string]$SourcePath = "$PSScriptRoot/../docs/art-prompts/crayon-hero-golden-walk-v1-sheet.png",
    [string]$OutputFolder = "$PSScriptRoot/../game/Assets/Art/Bosses/FairyKingdom/Movement"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Bitmap]::new((Resolve-Path -LiteralPath $SourcePath).Path)
try {
    if ($source.Width -ne 1774 -or $source.Height -ne 887) { throw 'Expected the approved 1774x887 sheet.' }
    $cell = 443.5
    # Uniform enlargement matches the awakened body's 1024px presentation canvas.
    $scale = 1.6
    for ($frame = 0; $frame -lt 8; $frame++) {
        $row = [int][Math]::Floor($frame / 4)
        $canvas = [System.Drawing.Bitmap]::new(1024, 1024, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($canvas)
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            # Preserve the approved lower-row +19px registration correction before uniform scaling.
            $dest = [System.Drawing.RectangleF]::new(157.2, (152 + $row * 19 * $scale), ($cell * $scale), ($cell * $scale))
            $sourceRect = [System.Drawing.RectangleF]::new((($frame % 4) * $cell), ($row * $cell), $cell, $cell)
            $graphics.DrawImage($source, $dest, $sourceRect, [System.Drawing.GraphicsUnit]::Pixel)
            for ($edge = 0; $edge -lt 1024; $edge++) {
                if ($canvas.GetPixel($edge, 0).A -ne 0 -or $canvas.GetPixel($edge, 1023).A -ne 0 -or
                    $canvas.GetPixel(0, $edge).A -ne 0 -or $canvas.GetPixel(1023, $edge).A -ne 0) {
                    throw "Frame $frame touches the canvas border."
                }
            }
            $canvas.Save((Join-Path $OutputFolder "CrayonHero_Golden_Walk_$frame.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $graphics.Dispose(); $canvas.Dispose() }
    }
}
finally { $source.Dispose() }
Write-Output 'Golden walk: eight 1024x1024 RGBA frames, uniform scale, lower-row registration and transparent borders verified.'
