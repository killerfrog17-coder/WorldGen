param(
    [ValidateSet('belongings','safety','world','material','daily','social','community','camp','dialogue')][string[]]$Cases=@('belongings','safety'),
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-z0-9-]+$')][string]$Prefix,
    [string]$World='artifacts/terrain-surfaces-20261107/playable.world'
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'"){throw 'A game is already open; leave it untouched.'}
foreach($case in $Cases){
    $out=Join-Path $repo ('artifacts/npc-overnight-20261109/'+$Prefix+'-'+$case)
    if(Test-Path -LiteralPath $out){throw 'Use a new prefix to retain earlier test evidence.'}
    New-Item -ItemType Directory -Force $out|Out-Null
    $arguments=@(('--worldgen-npc-'+$case+'-check'),('"'+(Join-Path $repo $World)+'"'),'--worldgen-entry-seed','3','--worldgen-ai-profile','hybrid','--worldgen-ai-model','qwen35','--worldgen-shared-gpu','--worldgen-output',('"'+$out+'"'),'--worldgen-library-root',('"'+(Join-Path $out 'library')+'"'),'-force-d3d11','-screen-width','1600','-screen-height','900','-screen-fullscreen','0','-monitor','2','-logFile',('"'+(Join-Path $out 'player.log')+'"'))
    # Visible review explicitly requested. All overnight game windows RIGHTMOST.
    $process=Start-Process -FilePath (Join-Path $repo 'builds/slot-b/WorldGen.exe') -ArgumentList $arguments -PassThru
    @{pid=$process.Id;startedUtc=[DateTime]::UtcNow;monitor='Rightmost';build=(Get-Content (Join-Path $repo 'builds/slot-b/build-inputs.json') -Raw|ConvertFrom-Json)}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $out 'owned-process.json')
    & (Join-Path $repo 'scripts/place-review-window.ps1') -ReviewProcessId $process.Id -Monitor Rightmost
    $process.WaitForExit()
    @{exitCode=$process.ExitCode;finishedUtc=[DateTime]::UtcNow}|ConvertTo-Json|Set-Content (Join-Path $out 'result.json')
    if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $out 'checks.txt'))){Get-Content (Join-Path $out 'failed-checks.txt') -ErrorAction SilentlyContinue;throw ('NPC '+$case+' failed: '+$out)}
    Get-Content (Join-Path $out 'checks.txt')
}
