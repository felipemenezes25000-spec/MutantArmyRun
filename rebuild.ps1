$ErrorActionPreference = "Continue"
$root = "C:\Users\Felipe\Downloads\jogo test"
& powershell -NoProfile -ExecutionPolicy Bypass -File "$root\run-factories.ps1"
if ($LASTEXITCODE -ne 0) { "FACTORIES FALHARAM"; exit 1 }

$unity = "D:\6000.4.8f1\Editor\Unity.exe"
$proj  = "$root\MutantArmyRun"
$t = 0
while ((Get-Process Unity -ErrorAction SilentlyContinue) -and $t -lt 30) { Start-Sleep 2; $t++ }
Start-Sleep 3
& $unity -batchmode -nographics -quit -projectPath $proj -executeMethod MutantArmy.Editor.BuildTools.BuildWindows -logFile "$root\rebuild-build.log" | Out-Null
"Build exit: $LASTEXITCODE"
Select-String -Path "$root\rebuild-build.log" -Pattern "\[BuildTools\]" | ForEach-Object { $_.Line.Trim() }
"=== REBUILD COMPLETO ==="
