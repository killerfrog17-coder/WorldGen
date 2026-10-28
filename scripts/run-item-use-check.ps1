param([string]$PlayerFolder='player-item-use-review',[string]$OutputName='combat-art-review')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$testOutput=Join-Path $repoRoot ('artifacts/overnight-items-20261028/'+$OutputName)
New-Item -ItemType Directory -Force -Path $testOutput | Out-Null
$existing=Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'" | Where-Object {$_.CommandLine -and $_.CommandLine.Contains('--worldgen-item-use-check')}
if($existing){throw 'An item-use check is still running; inspect its log before starting another.'}
$run=Start-Process -FilePath (Join-Path $repoRoot ('artifacts/'+$PlayerFolder+'/WorldGen.exe')) -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode','-screen-width','1280','-screen-height','800','-screen-fullscreen','0',
    '--worldgen-item-use-check',('"'+$repoRoot+'/unity/WorldGen.Unity/Assets/StreamingAssets/world/Earth.world"'),
    '--worldgen-output',('"'+$testOutput+'"'),
    '--worldgen-library-root',('"'+$repoRoot+'/artifacts/death-update/isolated-library"'),
    '-logFile',('"'+$testOutput+'/player.log"'))
Write-Output ('Item-use worker PID '+$run.Id+'; '+$testOutput+'/player.log')
