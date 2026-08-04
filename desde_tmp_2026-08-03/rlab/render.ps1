param(
  [Parameter(Mandatory=$true)][string]$MFile,   # absolute path to .m in tmp
  [Parameter(Mandatory=$true)][string]$Png      # absolute path to output png
)
$exe = "C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Calcpad-Lab\Symbolic.Wpf\bin\Release\net10.0-windows\CalcpadLab.exe"
if (Test-Path $Png) { Remove-Item $Png -Force }
$p = Start-Process -FilePath $exe -ArgumentList @("--shot", $Png, $MFile) -PassThru
# poll up to 60s for PNG
$deadline = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $deadline) {
  if (Test-Path $Png) {
    $len = (Get-Item $Png).Length
    if ($len -gt 0) { Start-Sleep -Milliseconds 700; break }
  }
  Start-Sleep -Milliseconds 500
}
Start-Sleep -Seconds 1
# kill ONLY CalcpadLab processes (their webview2 children die with them)
Get-Process -Name "CalcpadLab" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if (Test-Path $Png) {
  $len = (Get-Item $Png).Length
  Write-Output "OK $Png size=$len"
} else {
  Write-Output "FAIL no png"
}
