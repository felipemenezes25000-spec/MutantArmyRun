$unity = "D:\6000.4.8f1\Editor\Unity.exe"
$proj  = "C:\Users\Felipe\Downloads\jogo test\MutantArmyRun"
$root  = "C:\Users\Felipe\Downloads\jogo test"

function Wait-NoUnity {
    $t = 0
    while ((Get-Process Unity -ErrorAction SilentlyContinue) -and $t -lt 30) { Start-Sleep 2; $t++ }
    Start-Sleep 3
}

function Run-Tests($platform, $xml, $log) {
    Wait-NoUnity
    & $unity -batchmode -nographics -projectPath $proj -runTests -testPlatform $platform -testResults "$root\$xml" -logFile "$root\$log" | Out-Null
    if (Test-Path "$root\$xml") {
        [xml]$r = Get-Content "$root\$xml"
        $run = $r.'test-run'
        "{0,-9} total={1} passed={2} failed={3} result={4}" -f $platform, $run.total, $run.passed, $run.failed, $run.result
        if ([int]$run.failed -gt 0) {
            $r.SelectNodes("//test-case[@result='Failed']") | ForEach-Object { "  FALHOU: $($_.fullname)" }
        }
    } else { "{0,-9} SEM XML, ver {1}" -f $platform, $log }
}

Run-Tests "EditMode" "t-edit.xml" "t-edit.log"
Run-Tests "PlayMode" "t-play.xml" "t-play.log"

"--- dotnet ---"
foreach ($p in @("Domain.Gameplay.Tests","Domain.Flow.Tests","Domain.Persistence.Tests")) {
    $out = & dotnet test "$root\tests\$p" -v quiet --nologo 2>$null | Select-String "Aprovado!|Com falha|Failed!|Passed!"
    "$p : $out"
}
"=== TESTES OK ==="
