param(
    [ValidateSet('slot-a','slot-b')][string]$Slot='slot-b',
    [ValidatePattern('^[a-z0-9-]+$')][string]$Prefix='npc-report-20261104',
    [string]$World='artifacts/npc-20261103/current-190k.world',
    [ValidateSet('wet','collector','morning','cold','evening','sleep')]
    [string[]]$Cases=@('wet','collector','morning','cold','evening','sleep')
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$player=Join-Path $repo ('builds/'+$Slot+'/WorldGen.exe')
$fixture=Join-Path $repo $World
if(!(Test-Path -LiteralPath $fixture)){throw 'Generate the disposable human-world fixture first.'}
if(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'"){throw 'A game is already open; leave that session untouched.'}
$settings=@{
    wet=@('--worldgen-npc-context','wet','--worldgen-npc-hour','11')
    collector=@('--worldgen-npc-residence','CollectorCamps','--worldgen-npc-scale','0.3','--worldgen-npc-hour','14')
    morning=@('--worldgen-npc-context','temperate','--worldgen-npc-hour','7')
    cold=@('--worldgen-npc-context','cold','--worldgen-npc-scale','1','--worldgen-npc-hour','11')
    evening=@('--worldgen-npc-residence','AquaticResidence','--worldgen-npc-permanent','--worldgen-npc-context','coast','--worldgen-npc-hour','20')
    sleep=@('--worldgen-npc-context','temperate','--worldgen-npc-hour','23')
}
foreach($case in $Cases){
    $output=Join-Path $repo ('artifacts/npc-20261103/'+$Prefix+'-'+$case)
    if(Test-Path -LiteralPath $output){throw ('Keep prior captures; choose a new prefix: '+$output)}
    New-Item -ItemType Directory -Path $output|Out-Null
    $arguments=@('-screen-width','1536','-screen-height','864','-screen-fullscreen','0','--worldgen-npc-check',('"'+$fixture+'"'),'--worldgen-output',('"'+$output+'"'),'-logFile',('"'+$output+'/player.log"'))+$settings[$case]
    $process=Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output ($case+' capture PID '+$process.Id)
    $process.WaitForExit()
    [IO.File]::WriteAllText((Join-Path $output 'exit-code.txt'),[string]$process.ExitCode)
    if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $output 'checks.txt'))){throw ('NPC capture/check failed: '+$output)}
    Write-Output ($case+' capture PASS')
}
