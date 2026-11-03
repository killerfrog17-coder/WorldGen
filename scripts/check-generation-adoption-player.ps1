param([ValidateSet('generation','climate','playback')][string]$Mode='generation',[ValidateSet('slot-a','slot-b')][string]$Slot='slot-b',[string]$World='', [string]$Output='')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
if(!$Output){$Output=Join-Path $repoRoot ('artifacts/generation-adoption-20261103/'+$Mode+'-player')}
$Output=[IO.Path]::GetFullPath($Output);New-Item -ItemType Directory -Force $Output | Out-Null
$player=Join-Path $repoRoot ('builds/'+$Slot+'/WorldGen.exe')
$argsList=@('-screen-fullscreen','0','-screen-width','1600','-screen-height','900','-logFile',('"'+(Join-Path $Output 'player.log')+'"'),'--worldgen-output',('"'+$Output+'"'),'--worldgen-library-root',('"'+(Join-Path $Output 'library')+'"'))
if($Mode -eq 'generation'){$argsList+='--worldgen-generation-profile'}
else{
    if(!$World){throw 'A current test world is required.'}
    $argsList+=if($Mode -eq 'climate'){'--worldgen-dated-climate-check'}else{'--worldgen-regional-playback'}
    $argsList+=('"'+[IO.Path]::GetFullPath($World)+'"')
}
$run=Start-Process -FilePath $player -ArgumentList $argsList -PassThru -WindowStyle Hidden
if(!$run.WaitForExit(240000)){$run.Kill();throw ('Owned '+$Mode+' player timed out.')}
$run.WaitForExit();$run.Refresh();$code=$run.ExitCode
Set-Content -LiteralPath (Join-Path $Output 'exit-code.txt') -Value $code
if($code -ne 0){throw ('Owned '+$Mode+' player failed: '+$code)}
Write-Output ('PASS '+$Mode+' player '+$Output)
