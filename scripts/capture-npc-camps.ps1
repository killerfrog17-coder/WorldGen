param(
    [ValidateSet('slot-a','slot-b')][string]$Slot='slot-b',
    [ValidateSet('Leftmost','Rightmost')][string]$Monitor='Leftmost',
    [ValidatePattern('^[a-z0-9-]+$')][string]$Prefix='npc-report-20261104',
    [string]$World='artifacts/npc-20261103/current-190k.world',
    [ValidateSet('wet','collector','morning','cold','evening','sleep')]
    [string[]]$Cases=@('wet','collector','morning','cold','evening','sleep'),
    [switch]$Performance,
    [switch]$Crowd,
    [switch]$CrowdClose,
    [switch]$AutomaticComparison,
    [switch]$AllocationProfile,
    [string]$GcProbe="",
    [switch]$Cycle,
    [switch]$HaulFrame,
    [switch]$Trace,
    [switch]$RemovalProof,
    [ValidateRange(5,180)][int]$Seconds=30
)
$ErrorActionPreference='Stop'
if($Trace -and !$Performance){throw 'Frame tracing requires -Performance.'}
if($RemovalProof -and $Performance){throw 'Material-removal fixtures must not alter a performance scene.'}
if($AllocationProfile -and (!$Crowd -or !$Performance -or $AutomaticComparison)){throw 'AllocationProfile requires a controlled crowd, Performance, and no comparison.'}
$repo=Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName System.Windows.Forms
$npcReviewMonitor=[System.Windows.Forms.Screen]::AllScreens|Sort-Object {$_.Bounds.X} -Descending:($Monitor -eq 'Rightmost')|Select-Object -First 1
$npcReviewMonitorIndex=[regex]::Match($npcReviewMonitor.DeviceName,'\d+$').Value
if(!$npcReviewMonitorIndex){throw 'Cannot identify the selected NPC review monitor'}
$player=Join-Path $repo ('builds/'+$Slot+'/WorldGen.exe')
if($AllocationProfile -and !(Get-Content -Raw (Join-Path (Split-Path -Parent $player) 'build-inputs.json')|ConvertFrom-Json).Development){throw 'Allocation capture requires an explicitly built diagnostic DevelopmentPlayer.'}
$fixture=Join-Path $repo $World
if(!(Test-Path -LiteralPath $fixture)){throw 'Generate the disposable human-world fixture first.'}
if(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'"){throw 'A game is already open; leave that session untouched.'}
$settings=@{
    wet=@('--worldgen-npc-context','wet','--worldgen-npc-hour','11')
    collector=@('--worldgen-npc-residence','CollectorCamps','--worldgen-npc-scale','0.3','--worldgen-npc-hour','14')
    morning=@('--worldgen-npc-context','temperate','--worldgen-npc-hour','7')
    cold=@('--worldgen-npc-context','cold','--worldgen-npc-scale','0.1','--worldgen-npc-hour','11')
    evening=@('--worldgen-npc-context','coast','--worldgen-npc-hour','20')
    sleep=@('--worldgen-npc-context','temperate','--worldgen-npc-hour','23')
}
foreach($case in $Cases){
    $output=Join-Path $repo ('artifacts/npc-20261103/'+$Prefix+'-'+$case)
    if(Test-Path -LiteralPath $output){throw ('Keep prior captures; choose a new prefix: '+$output)}
    New-Item -ItemType Directory -Path $output|Out-Null
    $captureWidth=if($Crowd){'1920'}else{'1536'};$captureHeight=if($Crowd){'1080'}else{'864'}
    $arguments=@('-screen-width',$captureWidth,'-screen-height',$captureHeight,'-screen-fullscreen','0','-monitor',$npcReviewMonitorIndex,'--worldgen-npc-check',('"'+$fixture+'"'),'--worldgen-output',('"'+$output+'"'),'-logFile',('"'+$output+'/player.log"'))+$settings[$case]
    if($Performance){$arguments+=@('--worldgen-npc-perf','--worldgen-npc-perf-seconds',[string]$Seconds)}
    if($Crowd){if(!$Performance){throw 'Crowd is a controlled performance fixture only.'};$arguments+='--worldgen-npc-crowd'}
    if($AutomaticComparison){if(!$Crowd){throw 'AutomaticComparison requires controlled crowd.'};$arguments+='--worldgen-npc-automatic-comparison'}
    if($AllocationProfile){$arguments+='--worldgen-npc-allocation-profile'}
    if($CrowdClose){if(!$Crowd){throw 'CrowdClose requires the controlled crowd fixture.'};$arguments+='--worldgen-npc-crowd-close'}
    if($Cycle){$arguments+='--worldgen-npc-cycle'}
    if($HaulFrame){if(!$Cycle){throw 'HaulFrame is a controlled cycle fixture only.'};$arguments+='--worldgen-npc-haul-frame'}
    if($Trace){$arguments+='--worldgen-stutter-trace'}
    if($GcProbe){$probePath=[IO.Path]::GetFullPath((Join-Path $repo $GcProbe));if(!(Test-Path -LiteralPath $probePath)){throw 'Build the optional native GC probe first.'};$arguments+=@('--worldgen-gc-probe',('"'+$probePath+'"'))}
    if($RemovalProof){$arguments+='--worldgen-npc-removal-proof'}
    $process=Start-Process -FilePath $player -ArgumentList $arguments -PassThru
    & (Join-Path $repo 'scripts/place-review-window.ps1') -ReviewProcessId $process.Id -Monitor $Monitor
    @{pid=$process.Id;startedUtc=[DateTime]::UtcNow;monitor=$Monitor;build=(Get-Content (Join-Path $repo ('builds/'+$Slot+'/build-inputs.json')) -Raw|ConvertFrom-Json)}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $output 'owned-process.json')
    Write-Output ($case+' capture PID '+$process.Id)
    $process.WaitForExit()
    [IO.File]::WriteAllText((Join-Path $output 'exit-code.txt'),[string]$process.ExitCode)
    if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $output 'checks.txt'))){throw ('NPC capture/check failed: '+$output)}
    Write-Output ($case+' capture PASS')
}
