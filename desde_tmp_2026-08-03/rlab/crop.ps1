param([string]$In,[string]$Out,[int]$Y,[int]$H)
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile($In)
$w = $img.Width
if (($Y + $H) -gt $img.Height) { $H = $img.Height - $Y }
$rect = New-Object System.Drawing.Rectangle(0,$Y,$w,$H)
$bmp = New-Object System.Drawing.Bitmap($w,$H)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.DrawImage($img, (New-Object System.Drawing.Rectangle(0,0,$w,$H)), $rect, [System.Drawing.GraphicsUnit]::Pixel)
$bmp.Save($Out,[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose(); $img.Dispose()
Write-Output "cropped $Out w=$w h=$H"
