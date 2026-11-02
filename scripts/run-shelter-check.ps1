param([ValidateSet('slot-a','slot-b')][string]$PlayerFolder='slot-a',[string]$OutputName='player-check',[switch]$Water)
$ErrorActionPreference='Stop'
$shelterRepo=Split-Path -Parent $PSScriptRoot
$shelterOutput=Join-Path $shelterRepo ('artifacts/shelters-20261102/'+$OutputName)
if(Test-Path -LiteralPath (Join-Path $shelterOutput 'player.log')){throw 'Check output already exists; use a new scratch output name.'}
# Functional checks use their own library and synthetic input device. They can
# coexist with an owner session; they are not an uncontended FPS benchmark.
$shelterPlayer=Join-Path $shelterRepo ('builds/'+$PlayerFolder+'/WorldGen.exe')
$shelterBuild=Get-Content -Raw -LiteralPath (Join-Path (Split-Path -Parent $shelterPlayer) 'build-inputs.json') | ConvertFrom-Json
if($shelterBuild.Backend -notin @('Mono','IL2CPP')){throw 'Unknown or unfinished player backend.'}
New-Item -ItemType Directory -Force -Path $shelterOutput | Out-Null
$shelterArgs=@('-batchmode','-force-d3d11','-screen-width','1280','-screen-height','800','-screen-fullscreen','0',
    '--worldgen-item-use-check',('"'+$shelterRepo+'/unity/WorldGen.Unity/Assets/StreamingAssets/world/Earth.world"'),
    $(if($Water){'--worldgen-water-update-check'}else{'--worldgen-blueprint-check'}),'--worldgen-output',('"'+$shelterOutput+'"'),
    '--worldgen-library-root',('"'+$shelterRepo+'/artifacts/shelters-20261102/test-library"'),
    '-logFile',('"'+$shelterOutput+'/player.log"'))
$shelterRun=Start-Process -FilePath $shelterPlayer -ArgumentList $shelterArgs -WorkingDirectory $shelterRepo -WindowStyle Hidden -PassThru
Write-Output ('Shelter gameplay check PID '+$shelterRun.Id)
$shelterRun.WaitForExit()
$shelterChecks=if(Test-Path -LiteralPath (Join-Path $shelterOutput 'checks.txt')){@(Get-Content -LiteralPath (Join-Path $shelterOutput 'checks.txt'))}else{@()}
[ordered]@{backend=$shelterBuild.Backend;built_utc=$shelterBuild.BuiltUtc;exit_code=$shelterRun.ExitCode;checks=$shelterChecks.Count;finished_utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $shelterOutput 'result.json') -Encoding UTF8
if($shelterRun.ExitCode -ne 0 -or $shelterChecks.Count -eq 0){throw ('Shelter gameplay check failed: '+$shelterOutput)}
if(Select-String -LiteralPath (Join-Path $shelterOutput 'player.log') -Pattern 'NullReferenceException|InvalidOperationException|ArgumentException|Shader error|error CS' -Quiet){throw 'Shelter runtime log contains errors.'}
Write-Output ('PASS '+$shelterChecks.Count+' shelter gameplay checks')
