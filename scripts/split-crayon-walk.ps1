param(
    [string]$SourcePath = "$PSScriptRoot/../docs/art-prompts/crayon-hero-walk-v3-spaced-sheet.png",
    [string]$OutputFolder = "$PSScriptRoot/../game/Assets/Art/Bosses/FairyKingdom/Movement"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Bitmap]::new((Resolve-Path -LiteralPath $SourcePath).Path)
try {
    if ($source.Width -ne 2000 -or $source.Height -ne 887) {
        throw 'Expected the approved 2000x887 four-column, two-row sheet.'
    }
    for ($frame = 0; $frame -lt 8; $frame++) {
        $row = [int][Math]::Floor($frame / 4)
        $top = [int][Math]::Round($row * $source.Height / 2)
        $bottom = [int][Math]::Round(($row + 1) * $source.Height / 2)
        $rect = [System.Drawing.Rectangle]::new(($frame % 4) * 500, $top, 500, $bottom - $top)
        $crop = $source.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $canvas = [System.Drawing.Bitmap]::new(512, 512, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($canvas)
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            # Copy pixels without stretching. Equal square canvases preserve the 400 PPU registration.
            $graphics.DrawImageUnscaled($crop, 6, 34)
            for ($edge = 0; $edge -lt 512; $edge++) {
                if ($canvas.GetPixel($edge, 0).A -ne 0 -or $canvas.GetPixel($edge, 511).A -ne 0 -or
                    $canvas.GetPixel(0, $edge).A -ne 0 -or $canvas.GetPixel(511, $edge).A -ne 0) {
                    throw "Frame $frame touches its canvas edge."
                }
            }
            $canvas.Save((Join-Path $OutputFolder "CrayonHero_Walk_$frame.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $graphics.Dispose(); $canvas.Dispose(); $crop.Dispose() }
    }
}
finally { $source.Dispose() }
Write-Output 'Crayon walk: eight 512x512 RGBA frames copied at original pixel proportions, transparent borders verified.'
