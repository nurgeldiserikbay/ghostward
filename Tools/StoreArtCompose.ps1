# Картинки для карточки Google Play → Builds/store-art (папка не в git).
#  • icon-512.png — иконка 512×512 из Assets/GhostWard/Branding/AppIcon.png;
#  • feature-graphic-1024x500.jpg — логотип и сущности на тёмном фоне;
#  • screenshot-N.jpg — скриншоты с телефона из Builds/store-art/raw (png/jpg, по имени файла),
#    приводятся к 1080×1920 и получают подпись сверху (список $captions ниже, по порядку).
# Запуск: powershell -ExecutionPolicy Bypass -File tools/StoreArtCompose.ps1
Add-Type -AssemblyName System.Drawing
$proj = Split-Path -Parent $PSScriptRoot
$raw  = "$proj\Builds\store-art\raw"
$out  = "$proj\Builds\store-art"
New-Item -ItemType Directory -Force $out, $raw | Out-Null
Get-ChildItem $out -File | Remove-Item

$family = New-Object Drawing.FontFamily 'Georgia'
$jpeg = [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | ? MimeType -eq 'image/jpeg'
$quality = New-Object Drawing.Imaging.EncoderParameters 1
$quality.Param[0] = New-Object Drawing.Imaging.EncoderParameter ([Drawing.Imaging.Encoder]::Quality), 93L

function New-Canvas($w, $h) {
    $b = New-Object Drawing.Bitmap $w, $h, ([Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [Drawing.Graphics]::FromImage($b)
    $g.InterpolationMode = 'HighQualityBicubic'; $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'; $g.CompositingQuality = 'HighQuality'
    return $b, $g
}

# Одна строка по центру: шрифт уменьшается, пока текст не влезет; тень и тёмная обводка — читается на любом фоне.
function Draw-Text($g, $text, $size, $color, $centerX, $y, $maxWidth, $style = 1) {
    $probe = New-Object Drawing.Font $family, $size, ([Drawing.FontStyle]$style), ([Drawing.GraphicsUnit]::Pixel)
    $measured = $g.MeasureString($text, $probe, 100000).Width
    if ($measured -gt $maxWidth * .97) { $size = $size * $maxWidth * .97 / $measured }
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $format = New-Object Drawing.StringFormat; $format.Alignment = 'Center'
    $rect = New-Object Drawing.RectangleF ($centerX - $maxWidth / 2 - 200), $y, ($maxWidth + 400), ($size * 3)
    $path.AddString($text, $family, $style, $size, $rect, $format)
    $shadow = $path.Clone(); $m = New-Object Drawing.Drawing2D.Matrix; $m.Translate(0, $size * .06); $shadow.Transform($m)
    $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(150, 0, 0, 0))), $shadow)
    $pen = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(170, 8, 8, 10)), ($size * .08); $pen.LineJoin = 'Round'
    $g.DrawPath($pen, $path)
    $g.FillPath((New-Object Drawing.SolidBrush $color), $path)
}

function Shade-Top($g, $w, $h, $share) {
    $rect = New-Object Drawing.Rectangle 0, 0, $w, ([int]($h * $share))
    $brush = New-Object Drawing.Drawing2D.LinearGradientBrush $rect, ([Drawing.Color]::FromArgb(235, 6, 6, 9)), ([Drawing.Color]::FromArgb(0, 6, 6, 9)), 90
    $g.FillRectangle($brush, $rect)
}

# Картинка с прозрачностью alpha (0..1)
function Draw-Faded($g, $img, $x, $y, $w, $h, $alpha) {
    $cm = New-Object Drawing.Imaging.ColorMatrix; $cm.Matrix33 = $alpha
    $ia = New-Object Drawing.Imaging.ImageAttributes; $ia.SetColorMatrix($cm)
    $g.DrawImage($img, (New-Object Drawing.Rectangle ([int]$x), ([int]$y), ([int]$w), ([int]$h)), 0, 0, $img.Width, $img.Height, 'Pixel', $ia)
}

$white = [Drawing.Color]::FromArgb(255, 236, 236, 240)
$pale  = [Drawing.Color]::FromArgb(255, 196, 160, 160)

# ---------- скриншоты с телефона ----------
# Подписи — по порядку файлов в raw (отсортированы по имени). Лишние файлы получат последнюю пару без подписи.
$captions = @(
    @('Something is in your room',     'A horror game through your phone camera'),
    @('Find the clues',                'EMF reader, thermal camera, night vision, spirit box'),
    @('Name the entity',               '40 creatures from folklore around the world'),
    @('Drive it away before dawn',     'Salt, iron, a bell, a mirror: each fears something'),
    @('Keep a journal',                'Everything you learn stays in your bestiary'),
    @('Play in the dark',              'Lights off, headphones on')
)
# Кадры интерфейса из Unity (GhostWard → Скриншоты интерфейса для Google Play) называются N-ui-<экран>.png —
# им подпись по экрану; снимкам с телефона (1.png, 2.png …) — по порядку из $captions.
$byScreen = @{
    'menu'  = $captions[0]
    'clues' = $captions[1]
    'entry' = $captions[2]
    'items' = $captions[3]
    'list'  = $captions[4]
}
$shots = Get-ChildItem $raw -File | ? { $_.Extension -match '^\.(png|jpe?g)$' } | Sort-Object Name
$n = 1
foreach ($f in $shots) {
    $screen = if ($f.BaseName -match '-ui-(.+)$') { $Matches[1] } else { $null }
    $caption = if ($screen -and $byScreen.ContainsKey($screen)) { $byScreen[$screen] } elseif ($n -le $captions.Count) { $captions[$n - 1] } else { $null }
    $src = [Drawing.Image]::FromFile($f.FullName)
    $b, $g = New-Canvas 1080 1920
    if ($screen) {
        # кадр интерфейса: подпись — отдельной полосой сверху, экран под ней уменьшен с тонкой рамкой
        $g.Clear([Drawing.Color]::FromArgb(255, 9, 9, 12))
        $top = 320; $h = 1920 - $top - 40; $w = $src.Width * $h / $src.Height; $x = (1080 - $w) / 2
        $g.DrawImage($src, [int]$x, $top, [int]$w, [int]$h)
        $g.DrawRectangle((New-Object Drawing.Pen ([Drawing.Color]::FromArgb(255, 60, 70, 60)), 3), [int]$x, $top, [int]$w, [int]$h)
    } else {
        # снимок с телефона: заполнить кадр 9:16, лишнее обрезать по центру
        $scale = [Math]::Max(1080 / $src.Width, 1920 / $src.Height)
        $w = $src.Width * $scale; $h = $src.Height * $scale
        $g.DrawImage($src, [int]((1080 - $w) / 2), [int]((1920 - $h) / 2), [int]$w, [int]$h)
    }
    if ($caption) {
        if (-not $screen) { Shade-Top $g 1080 1920 .3 }
        Draw-Text $g $caption[0] 84 $white 540 90 1000
        Draw-Text $g $caption[1] 44 $pale 540 210 1000 0
    }
    $b.Save(("$out\screenshot-{0}.jpg" -f $n), $jpeg, $quality)
    $g.Dispose(); $b.Dispose(); $src.Dispose(); $n++
}

# ---------- баннер 1024×500 ----------
$b, $g = New-Canvas 1024 500
$g.Clear([Drawing.Color]::FromArgb(255, 9, 9, 12))
# тусклое красное свечение за сущностями
$glow = New-Object Drawing.Drawing2D.GraphicsPath; $glow.AddEllipse(520, -60, 620, 620)
$pb = New-Object Drawing.Drawing2D.PathGradientBrush $glow
$pb.CenterColor = [Drawing.Color]::FromArgb(120, 70, 12, 14); $pb.SurroundColors = @([Drawing.Color]::FromArgb(0, 9, 9, 12))
$g.FillPath($pb, $glow)
# сущности справа: дальняя — бледнее
foreach ($e in @(@('Shade', 560, 70, 300, .35), @('Albasty', 820, 40, 330, .5), @('Ghost', 660, 20, 380, .95))) {
    $img = [Drawing.Image]::FromFile("$proj\Assets\Resources\Entities\$($e[0]).png")
    $h = $e[3] * $img.Height / $img.Width
    Draw-Faded $g $img $e[1] $e[2] $e[3] $h $e[4]
    $img.Dispose()
}
# затемнение к левому краю — под логотип
$rect = New-Object Drawing.Rectangle 0, 0, 800, 500
$lb = New-Object Drawing.Drawing2D.LinearGradientBrush $rect, ([Drawing.Color]::FromArgb(255, 9, 9, 12)), ([Drawing.Color]::FromArgb(0, 9, 9, 12)), ([single]0)
$blend = New-Object Drawing.Drawing2D.Blend 4
$blend.Factors = [single[]](0, 0, .7, 1); $blend.Positions = [single[]](0, .55, .8, 1)
$lb.Blend = $blend
$g.FillRectangle($lb, (New-Object Drawing.Rectangle 0, 0, 799, 500))
$bottom = New-Object Drawing.Rectangle 0, 360, 1024, 141
$bb = New-Object Drawing.Drawing2D.LinearGradientBrush $bottom, ([Drawing.Color]::FromArgb(0, 9, 9, 12)), ([Drawing.Color]::FromArgb(230, 9, 9, 12)), 90
$g.FillRectangle($bb, $bottom)
$logo = [Drawing.Image]::FromFile("$proj\Assets\GhostWard\Resources\Branding\Logo.png")
$lw = 520; $lh = [int]($logo.Height * $lw / $logo.Width)
$g.DrawImage($logo, 30, 70, $lw, $lh)
Draw-Text $g 'Something is in your room' 34 $pale 290 (70 + $lh + 4) 500 2
$b.Save("$out\feature-graphic-1024x500.jpg", $jpeg, $quality)
$g.Dispose(); $b.Dispose(); $logo.Dispose()

# ---------- иконка 512×512 ----------
$src = [Drawing.Image]::FromFile("$proj\Assets\GhostWard\Branding\AppIcon.png")
$b = New-Object Drawing.Bitmap 512, 512, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [Drawing.Graphics]::FromImage($b); $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
$g.DrawImage($src, 0, 0, 512, 512); $b.Save("$out\icon-512.png", [Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $b.Dispose(); $src.Dispose()

"скриншотов с телефона: $($shots.Count)"
Get-ChildItem $out -File | % { "{0}  {1} KB" -f $_.Name, [math]::Round($_.Length / 1KB) }
