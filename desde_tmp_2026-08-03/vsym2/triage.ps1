$cli = "C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Calcpad-Symbolic\Symbolic.Cli\bin\Release\net10.0\Cli.exe"
$ex = "C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Calcpad-Symbolic\Examples"
$outdir = "C:\tmp\vsym2\html"
$files = Get-ChildItem -Path $ex -Recurse -Filter *.cpds
$patterns = @('class="err"', "class='err'", 'Error on line', 'Undefined', 'Unexpected', 'Argument must be scalar', 'is not defined', 'Missing operand', 'Invalid ')
$results = @()
$i = 0
foreach ($f in $files) {
    $i++
    $out = Join-Path $outdir ("f$i.html")
    $cliout = & $cli $f.FullName $out 2>&1 | Out-String
    $flagged = @()
    $exitfail = ($LASTEXITCODE -ne 0)
    if ($cliout -match 'Error|Exception|Undefined|Unexpected') { $flagged += "CLIOUT" }
    if (Test-Path $out) {
        $h = Get-Content $out -Raw
        foreach ($p in $patterns) {
            if ($h -like "*$p*") { $flagged += $p }
        }
    } else {
        $flagged += "NO_HTML"
    }
    if ($exitfail) { $flagged += "EXITFAIL" }
    if ($flagged.Count -gt 0) {
        $results += [PSCustomObject]@{ Idx=$i; File=$f.FullName; Flags=($flagged -join '; '); CliOut=($cliout.Trim()) }
    }
}
"===TOTAL=== $($files.Count)"
"===FLAGGED=== $($results.Count)"
$results | ForEach-Object { "IDX $($_.Idx) | $($_.Flags)`n    $($_.File)`n    CLI: $($_.CliOut)" }
