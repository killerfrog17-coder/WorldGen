param([ValidateSet('Shader','Combat','Items')][string]$Mode='Shader',[Parameter(Mandatory=$true)][string]$OutputName)
$ErrorActionPreference='Stop'
$feedbackRepo=Split-Path -Parent $PSScriptRoot
$feedbackTask=Join-Path $feedbackRepo 'artifacts/world-feedback-20261031'
$feedbackOutput=Join-Path $feedbackTask $OutputName
if(Test-Path -LiteralPath (Join-Path $feedbackOutput 'player.log')){throw 'Use a fresh evidence output name.'}
$feedbackPlayer=Join-Path $feedbackRepo 'artifacts/player-world-feedback/WorldGen.exe'
$feedbackBuild=Join-Path (Split-Path -Parent $feedbackPlayer) 'build-inputs.json'
if(!(Test-Path -LiteralPath $feedbackBuild)){throw 'Player package is incomplete.'}
$feedbackInputs=Get-Content -Raw -LiteralPath $feedbackBuild | ConvertFrom-Json
if($feedbackInputs.Backend -ne 'IL2CPP'){throw 'Native IL2CPP required.'}
New-Item -ItemType Directory -Path $feedbackOutput -Force | Out-Null
$feedbackArgs=@('-batchmode','-force-d3d11','-screen-width','1280','-screen-height','800','--worldgen-output',('"'+$feedbackOutput+'"'),'-logFile',('"'+$feedbackOutput+'/player.log"'))
if($Mode -eq 'Shader'){$feedbackArgs+='--worldgen-feedback-shader-check'}
else{
    $feedbackArgs+=@('--worldgen-item-use-check',('"'+$feedbackRepo+'/unity/WorldGen.Unity/Assets/StreamingAssets/world/Earth.world"'),'--worldgen-library-root',('"'+$feedbackTask+'/native-library"'))
    if($Mode -eq 'Combat'){$feedbackArgs+='--worldgen-feedback-check'}
}
$feedbackRun=Start-Process -FilePath $feedbackPlayer -ArgumentList $feedbackArgs -WorkingDirectory $feedbackRepo -WindowStyle Hidden -PassThru
Write-Output ('Owned '+$Mode+' check PID '+$feedbackRun.Id)
$feedbackRun.WaitForExit()
$feedbackCount=if(Test-Path -LiteralPath (Join-Path $feedbackOutput 'checks.txt')){@(Get-Content -LiteralPath (Join-Path $feedbackOutput 'checks.txt')).Count}else{0}
[ordered]@{pid=$feedbackRun.Id;mode=$Mode;exit_code=$feedbackRun.ExitCode;checks=$feedbackCount;built_utc=$feedbackInputs.BuiltUtc;finished_utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $feedbackOutput 'result.json') -Encoding UTF8
if($feedbackRun.ExitCode -ne 0 -or $feedbackCount -eq 0){throw ('Native check failed: '+$feedbackOutput)}
if(Select-String -LiteralPath (Join-Path $feedbackOutput 'player.log') -Pattern 'NullReferenceException|InvalidOperationException|ArgumentException|Shader error|error CS' -Quiet){throw 'Native log contains runtime errors.'}
Write-Output ('PASS '+$feedbackCount+' '+$Mode+' checks')
