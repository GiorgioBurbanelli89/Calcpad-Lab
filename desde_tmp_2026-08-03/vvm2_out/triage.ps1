$CLI = "C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\calcpad_original_ned\Calcpad.Cli\bin\Release\net10.0\Cli.exe"
$BASE = "C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\calcpad_original_ned\Examples"
$OUT = "C:\tmp\vvm2_out"
$flagged = @()
$ok = @()
$map = @()
$patterns = 'class="err"|Error on line|Undefined|Unexpected variable|Argument must be scalar'
$files = Get-ChildItem -Path $BASE -Recurse -Filter *.cpd
$n = 0
foreach ($f in $files) {
  $n++
  $md5 = [System.Security.Cryptography.MD5]::Create()
  $bytes = [System.Text.Encoding]::UTF8.GetBytes($f.FullName.Replace('\','/'))
  $hash = ($md5.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join ''
  $base = $hash.Substring(0,12)
  $html = Join-Path $OUT "$base.html"
  if (Test-Path $html) { Remove-Item $html -Force }
  & $CLI $f.FullName $html *> $null
  $map += "$base|$($f.FullName)"
  if (Test-Path $html) {
    $content = Get-Content $html -Raw
    if ($content -match $patterns) { $flagged += $f.FullName } else { $ok += $f.FullName }
  } else {
    $flagged += "NOHTML|$($f.FullName)"
  }
}
$flagged | Set-Content "$OUT\_flagged.txt"
$ok | Set-Content "$OUT\_ok.txt"
$map | Set-Content "$OUT\_map.txt"
"DONE total=$n flagged=$($flagged.Count) ok=$($ok.Count)" | Set-Content "$OUT\_status.txt"
