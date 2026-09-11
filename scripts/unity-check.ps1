param([string]$Unity='C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe',[switch]$BuildPlayer,[switch]$PlayerCheck)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$project=Join-Path $repoRoot 'unity/WorldGen.Unity'
$artifacts=Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Force $artifacts | Out-Null
$method=if($BuildPlayer){'WorldGenBatch.BuildPlayer'}else{'WorldGenBatch.Validate'}
$log=Join-Path $artifacts $(if($BuildPlayer){'unity-build.log'}else{'unity-validation.log'})
$arguments=@('-batchmode','-nographics','-quit','-projectPath',('"'+$project+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"'))
$run=Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$run.WaitForExit()
if($run.ExitCode -ne 0){throw "Unity failed. Inspect $log"}
if($PlayerCheck){
    $output=Join-Path $artifacts 'unity-player'
    $player=Start-Process -FilePath (Join-Path $artifacts 'player/WorldGen.exe') -ArgumentList @('-batchmode','-nographics','--worldgen-check','--worldgen-output',('"'+$output+'"'),'-logFile',('"'+(Join-Path $artifacts 'player-check.log')+'"')) -WindowStyle Hidden -PassThru
    $player.WaitForExit()
    if($player.ExitCode -ne 0){throw 'Player checks failed'}
    foreach($golden in Get-ChildItem (Join-Path $repoRoot 'tests/golden') -Filter '*.hashes.json'){
        $expected=Get-Content -Raw $golden.FullName | ConvertFrom-Json
        $actual=Get-Content -Raw (Join-Path $output $golden.Name) | ConvertFrom-Json
        if(($expected.Hash -join ',') -ne ($actual.Hash -join ',')){throw "Player hash mismatch: $($golden.Name)"}
    }
    Write-Host 'All player stage hashes match CLI golden corpus.'
}
