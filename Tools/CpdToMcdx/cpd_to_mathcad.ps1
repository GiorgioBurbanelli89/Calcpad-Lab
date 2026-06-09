<#
  Punto de entrada único para el editor (Hekatan Sheet): toma una hoja .cpd
  (regiones de texto = línea con comilla inicial  "texto ;  regiones math = la
  expresión cruda), la convierte a .mcdx y la abre en Mathcad Prime.

  Uso:   powershell -File cpd_to_mathcad.ps1  C:\ruta\hoja.cpd  [C:\salida.mcdx]
  Sale 0 si todo bien; imprime la ruta del .mcdx generado.
#>
param(
  [Parameter(Mandatory = $true)][string]$Cpd,
  [string]$Mcdx
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Cpd)) { Write-Error "no existe: $Cpd"; exit 1 }
if (-not $Mcdx) { $Mcdx = [IO.Path]::ChangeExtension($Cpd, '.mcdx') }

# 1) localizar el conversor (build Release del repo)
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$conv = Join-Path $here 'bin\Release\net10.0\CpdToMcdx.dll'
if (-not (Test-Path $conv)) { Write-Error "falta CpdToMcdx.dll (compilá CpdToMcdx en Release)"; exit 2 }

# 2) .cpd -> .mcdx  (sin abrir navegador)
& dotnet $conv $Cpd $Mcdx --version 10.0 --no-preview | Out-Null
if (-not (Test-Path $Mcdx)) { Write-Error "no se generó el .mcdx"; exit 3 }

# 3) abrir en Mathcad por asociación de archivo (arranca Mathcad o reusa instancia)
Start-Process $Mcdx
Write-Output $Mcdx
exit 0
