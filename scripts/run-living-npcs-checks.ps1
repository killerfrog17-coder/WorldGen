param(
    [ValidateSet('slot-a','slot-b')][string]$Slot='slot-a',
    [ValidatePattern('^[a-z0-9-]+$')][string]$Prefix='courts',
    [string]$World='artifacts/npc-20261103/current-190k.world',
    [string]$StartWorld='artifacts/npc-20261103/startup-0.world'
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$player=Join-Path $repoRoot ('builds/'+$Slot+'/WorldGen.exe')
# These are disposable fixtures produced by Core.Tests --human-fixture from
# a completed planet, at 190000 and 0 years. No player or source asset is copied.
foreach($inputPath in @($World,$StartWorld)){
    if(!(Test-Path -LiteralPath (Join-Path $repoRoot $inputPath))){throw ('Generate the missing test fixture first: '+$inputPath)}
}
if(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'" | Where-Object {$_.ExecutablePath -eq $player}){throw 'This slot is already running; leave that session untouched.'}
foreach($case in @('poses','wet','cold','aquatic','collector','start')){
    $output=Join-Path $repoRoot ('artifacts/npc-20261103/'+$Prefix+'-'+$case)
    if(Test-Path -LiteralPath (Join-Path $output 'exit-code.txt')){throw ('Keep completed evidence intact; choose a new -Prefix: '+$output)}
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $arguments=@('-screen-width','1536','-screen-height','864','-screen-fullscreen','0','--worldgen-output',('"'+$output+'"'),'-logFile',('"'+$output+'/player.log"'))
    if($case -eq 'poses'){$arguments+='--worldgen-npc-pose-check'}
    else{
        $fixture=Join-Path $repoRoot $(if($case -eq 'start'){$StartWorld}else{$World})
        $arguments+=@('--worldgen-npc-check',('"'+$fixture+'"'))
        switch($case){
            'wet' {$arguments+=@('--worldgen-npc-context','wet','--worldgen-npc-hour','11','--worldgen-npc-walk','--worldgen-npc-cycle','--worldgen-npc-perf')}
            'cold' {$arguments+=@('--worldgen-npc-context','cold','--worldgen-npc-scale','1','--worldgen-npc-hour','7','--worldgen-npc-walk','--worldgen-npc-cycle')}
            'aquatic' {$arguments+=@('--worldgen-npc-residence','AquaticResidence','--worldgen-npc-permanent','--worldgen-npc-context','coast','--worldgen-npc-hour','20','--worldgen-npc-cycle')}
            'collector' {$arguments+=@('--worldgen-npc-residence','CollectorCamps','--worldgen-npc-scale','0.3','--worldgen-npc-hour','14','--worldgen-npc-cycle','--worldgen-npc-walk')}
            'start' {$arguments+=@('--worldgen-npc-context','temperate','--worldgen-npc-hour','9')}
        }
    }
    $process=Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output ($Prefix+'-'+$case+' PID '+$process.Id)
    $process.WaitForExit()
    [IO.File]::WriteAllText((Join-Path $output 'exit-code.txt'),[string]$process.ExitCode)
    Write-Output ($Prefix+'-'+$case+' EXIT '+$process.ExitCode)
}
