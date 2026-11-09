param(
    [ValidateSet('belongings','safety','world','material','daily','social','community','camp','dialogue','encounter','routine','navigation')][string[]]$Cases=@('belongings','safety'),
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-z0-9-]+$')][string]$Prefix,
    [string]$World='artifacts/terrain-surfaces-20261107/playable.world',
    [switch]$ContinueOnFailure
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName System.Windows.Forms
$npcReviewMonitor=[System.Windows.Forms.Screen]::AllScreens|Sort-Object {$_.Bounds.X} -Descending|Select-Object -First 1
$npcReviewMonitorIndex=[regex]::Match($npcReviewMonitor.DeviceName,'\d+$').Value
if(!$npcReviewMonitorIndex){throw 'Cannot identify the rightmost NPC review monitor'}
if(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'"){throw 'A game is already open; leave it untouched.'}
$failedCases=@()
foreach($case in $Cases){
    $out=Join-Path $repo ('artifacts/npc-overnight-20261109/'+$Prefix+'-'+$case)
    if(Test-Path -LiteralPath $out){throw 'Use a new prefix to retain earlier test evidence.'}
    New-Item -ItemType Directory -Force $out|Out-Null
    $testName=if($case -eq 'dialogue'){'grounded-dialogue'}else{$case}
    $arguments=@(('--worldgen-npc-'+$testName+'-check'),('"'+(Join-Path $repo $World)+'"'),'--worldgen-entry-seed','3','--worldgen-ai-profile','hybrid','--worldgen-ai-model','qwen35','--worldgen-shared-gpu','--worldgen-output',('"'+$out+'"'),'--worldgen-library-root',('"'+(Join-Path $out 'library')+'"'),'-force-d3d11','-screen-width','1600','-screen-height','900','-screen-fullscreen','0','-monitor',$npcReviewMonitorIndex,'-logFile',('"'+(Join-Path $out 'player.log')+'"'))
    # Visible review explicitly requested. All overnight game windows RIGHTMOST.
    $process=Start-Process -FilePath (Join-Path $repo 'builds/slot-b/WorldGen.exe') -ArgumentList $arguments -PassThru
    @{pid=$process.Id;startedUtc=[DateTime]::UtcNow;monitor='Rightmost';build=(Get-Content (Join-Path $repo 'builds/slot-b/build-inputs.json') -Raw|ConvertFrom-Json)}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $out 'owned-process.json')
    & (Join-Path $repo 'scripts/place-review-window.ps1') -ReviewProcessId $process.Id -Monitor Rightmost
    $process.WaitForExit()
    @{exitCode=$process.ExitCode;finishedUtc=[DateTime]::UtcNow}|ConvertTo-Json|Set-Content (Join-Path $out 'result.json')
    if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $out 'checks.txt'))){Get-Content (Join-Path $out 'failed-checks.txt') -ErrorAction SilentlyContinue;if($ContinueOnFailure){$failedCases+=$case;continue};throw ('NPC '+$case+' failed: '+$out)}
    Get-Content (Join-Path $out 'checks.txt')
    if((Get-Content (Join-Path $out 'npc-check-kind.txt') -Raw).Trim() -ne ('--worldgen-npc-'+$testName+'-check')){throw 'Wrong or missing native test completion receipt.'}
}

if($failedCases.Count -gt 0){throw ('NPC checks failed (all independent cases were attempted): '+($failedCases -join ', '))}
