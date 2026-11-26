<#
    Renders the setup images from the brand assets in the repo (src/Gym.UI/Themes/Logo.xaml and the Alexandria
    font): the wizard images (PNG, Deep Teal), the small top-right images and titanfit.ico.
    Runs on Windows PowerShell 5.1 (WPF, STA) on the CI runner:
      powershell -NoProfile -File installer/branding/make-branding.ps1 -RepoDir . -OutDir artifacts/branding
#>
param(
    [Parameter(Mandatory = $true)][string]$RepoDir,
    [Parameter(Mandatory = $true)][string]$OutDir
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$RepoDir = (Resolve-Path $RepoDir).Path
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

$logo = [Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $RepoDir 'src\Gym.UI\Themes\Logo.xaml')))
$mark = $logo['Drawing.LogoMark']
if ($null -eq $mark) { throw 'Drawing.LogoMark not found in Logo.xaml' }

$fontFolder = New-Object Uri(((Join-Path $RepoDir 'src\Gym.UI\Fonts') -replace '\\', '/') + '/')
$alexandria = New-Object Windows.Media.FontFamily($fontFolder, './#Alexandria')
$typeface = New-Object Windows.Media.Typeface($alexandria, [Windows.FontStyles]::Normal, [Windows.FontWeights]::ExtraBold,
    [Windows.FontStretches]::Normal)
$glyphs = $null
if (-not $typeface.TryGetGlyphTypeface([ref]$glyphs) -or $glyphs.FontUri.AbsolutePath -notmatch 'Alexandria') {
    throw 'The Alexandria font did not load.'
}
Write-Host "Wordmark font: $($glyphs.FontUri)"

function Get-Color([string]$Hex) { [Windows.Media.ColorConverter]::ConvertFromString($Hex) }
function Get-Brush([string]$Hex) {
    $brush = New-Object Windows.Media.SolidColorBrush(Get-Color $Hex)
    $brush.Freeze()
    $brush
}

$white = Get-Brush '#EEF7F9'
$brand = Get-Brush '#22D8F0'
$panel = Get-Brush '#0F2731'
$line = Get-Brush '#24505D'

# Draws the logo mark centred in the given rectangle, keeping its aspect ratio.
function Add-Mark($Context, [double]$X, [double]$Y, [double]$Width, [double]$Height) {
    $bounds = $mark.Bounds
    $scale = [Math]::Min($Width / $bounds.Width, $Height / $bounds.Height)
    $offsetX = $X + ($Width - $bounds.Width * $scale) / 2 - $bounds.X * $scale
    $offsetY = $Y + ($Height - $bounds.Height * $scale) / 2 - $bounds.Y * $scale
    $group = New-Object Windows.Media.TransformGroup
    $group.Children.Add((New-Object Windows.Media.ScaleTransform($scale, $scale)))
    $group.Children.Add((New-Object Windows.Media.TranslateTransform($offsetX, $offsetY)))
    $Context.PushTransform($group)
    $Context.DrawDrawing($mark)
    $Context.Pop()
}

# "TITAN" in white and "FIT" in brand cyan, centred at $CenterX, as wide as $MaxWidth at most.
function Add-Wordmark($Context, [double]$CenterX, [double]$Top, [double]$MaxWidth) {
    $size = 100.0
    $text = New-Object Windows.Media.FormattedText('TITANFIT', [Globalization.CultureInfo]::InvariantCulture,
        [Windows.FlowDirection]::LeftToRight, $typeface, $size, $white, 1.0)
    $size = $size * $MaxWidth / $text.WidthIncludingTrailingWhitespace
    $text = New-Object Windows.Media.FormattedText('TITANFIT', [Globalization.CultureInfo]::InvariantCulture,
        [Windows.FlowDirection]::LeftToRight, $typeface, $size, $white, 1.0)
    $text.SetForegroundBrush($brand, 5, 3)
    $Context.DrawText($text, (New-Object Windows.Point(($CenterX - $text.WidthIncludingTrailingWhitespace / 2), $Top)))
    $text.Height
}

function Save-Png([Windows.Media.Visual]$Visual, [int]$Width, [int]$Height, [string]$Name) {
    $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap($Width, $Height, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($Visual)
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object IO.MemoryStream
    $encoder.Save($stream)
    $bytes = $stream.ToArray()
    if ($Name) { [IO.File]::WriteAllBytes((Join-Path $OutDir $Name), $bytes) }
    , $bytes   # one byte[] object, not a stream of bytes
}

# Wizard image (left side of the first and last page): sidebar gradient, logo mark, wordmark.
function New-WizardImage([int]$Width, [int]$Height) {
    $visual = New-Object Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    $gradient = New-Object Windows.Media.LinearGradientBrush((Get-Color '#0C2830'), (Get-Color '#0A1F26'), 90.0)
    $dc.DrawRectangle($gradient, $null, (New-Object Windows.Rect(0, 0, $Width, $Height)))
    $accent = New-Object Windows.Media.LinearGradientBrush((Get-Color '#22D8F0'), (Get-Color '#14C2B8'), 0.0)
    $dc.DrawRectangle($accent, $null, (New-Object Windows.Rect(0, 0, $Width, [Math]::Max(2, $Height / 160))))
    $markSize = $Width * 0.56
    $markTop = $Height * 0.25
    Add-Mark $dc (($Width - $markSize) / 2) $markTop $markSize $markSize
    [void](Add-Wordmark $dc ($Width / 2) ($markTop + $markSize + $Height * 0.05) ($Width * 0.74))
    $dc.Close()
    [void](Save-Png $visual $Width $Height "wizard-${Width}x${Height}.png")
}

# Small image (top right of the inner pages): the mark on a rounded panel tile.
function New-Tile([int]$Size, [string]$Name) {
    $visual = New-Object Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    $radius = $Size * 0.22
    $border = New-Object Windows.Media.Pen($line, [Math]::Max(1.0, $Size / 64))
    $inset = $border.Thickness / 2
    $dc.DrawRoundedRectangle($panel, $border, (New-Object Windows.Rect($inset, $inset, ($Size - 2 * $inset), ($Size - 2 * $inset))), $radius, $radius)
    $pad = $Size * 0.14
    Add-Mark $dc $pad $pad ($Size - 2 * $pad) ($Size - 2 * $pad)
    $dc.Close()
    Save-Png $visual $Size $Size $Name
}

foreach ($size in @(@(202, 386), @(269, 515), @(336, 643), @(430, 824))) { New-WizardImage $size[0] $size[1] }
foreach ($size in 58, 77, 97, 124, 159) { [void](New-Tile $size "small-$size.png") }

# titanfit.ico: PNG-compressed entries (Windows Vista and later).
$iconSizes = 16, 24, 32, 48, 64, 256
$images = @()
foreach ($size in $iconSizes) { $images += , (New-Tile $size $null) }
$icon = New-Object IO.MemoryStream
$writer = New-Object IO.BinaryWriter($icon)
$writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$iconSizes.Count)
$offset = 6 + 16 * $iconSizes.Count
for ($i = 0; $i -lt $iconSizes.Count; $i++) {
    $dimension = $iconSizes[$i] % 256   # 0 means 256
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([UInt16]1); $writer.Write([UInt16]32)
    $writer.Write([UInt32]$images[$i].Length); $writer.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write([byte[]]$image) }
$writer.Flush()
[IO.File]::WriteAllBytes((Join-Path $OutDir 'titanfit.ico'), $icon.ToArray())

Get-ChildItem $OutDir | ForEach-Object { Write-Host ('{0,-24} {1,8:N0} bytes' -f $_.Name, $_.Length) }
