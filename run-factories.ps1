$ErrorActionPreference = "Stop"
$unity = "D:\6000.4.8f1\Editor\Unity.exe"
$proj  = "C:\Users\Felipe\Downloads\jogo test\MutantArmyRun"
$logdir = "C:\Users\Felipe\Downloads\jogo test"

function Wait-NoUnity {
    $tries = 0
    while ((Get-Process Unity -ErrorAction SilentlyContinue) -and $tries -lt 30) {
        Start-Sleep -Seconds 2; $tries++
    }
    Start-Sleep -Seconds 3   # deixa workers/lockfile liberarem
}

function Run-Method($name, $method, $log) {
    Wait-NoUnity
    & $unity -batchmode -nographics -quit -projectPath $proj -executeMethod $method -logFile "$logdir\$log" | Out-Null
    $code = $LASTEXITCODE
    $errs = (Select-String -Path "$logdir\$log" -Pattern "error CS" -ErrorAction SilentlyContinue | Measure-Object).Count
    $exc  = (Select-String -Path "$logdir\$log" -Pattern "Exception:|threw an exception|NullReferenceException" -ErrorAction SilentlyContinue | Measure-Object).Count
    "{0,-28} exit={1} errCS={2} exc={3}" -f $name, $code, $errs, $exc
    if ($code -ne 0) { "  >> ABORTOU em $name (exit $code)"; exit 1 }
}

Run-Method "1.ImportConfigurator" "MutantArmy.Editor.ImportConfigurator.ConfigureAll" "fac-1-import.log"
Run-Method "2.MvpContentFactory"  "MutantArmy.Editor.MvpContentFactory.CreateAll"     "fac-2-content.log"
Run-Method "3.UiSkinFactory"      "MutantArmy.Editor.UiSkinFactory.BuildAll"          "fac-3-uiskin.log"
Run-Method "4.ProjectSetup"       "MutantArmy.Editor.ProjectSetup.SetupProject"       "fac-4-setup.log"
Run-Method "5.GreyboxFactory"     "MutantArmy.Editor.GreyboxFactory.BuildAll"         "fac-5-greybox.log"
Run-Method "6.UnitVisualFactory"  "MutantArmy.Editor.UnitVisualFactory.BuildAll"      "fac-6-units.log"
Run-Method "7.WorldVisualFactory" "MutantArmy.Editor.WorldVisualFactory.BuildAll"     "fac-7-world.log"
Run-Method "8.JuiceFactory"       "MutantArmy.Editor.JuiceFactory.BuildAll"           "fac-8-juice.log"
"=== TODAS AS FACTORIES OK ==="
