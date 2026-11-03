param(
    [ValidatePattern('^[a-z0-9-]+$')][string]$Prefix='landscape-perf',
    [string]$World='artifacts/npc-20261103/current-190k.world'
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$fixture=Join-Path $repoRoot $World
if(!(Test-Path -LiteralPath $fixture)){throw 'Generate the disposable world fixture first.'}
if(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'"){throw 'A game session is open; keep that session untouched and run this comparison once it is closed.'}
foreach($case in @(@{Name='baseline';Slot='slot-a'},@{Name='candidate';Slot='slot-b'})){
    $player=Join-Path $repoRoot ('builds/'+$case.Slot+'/WorldGen.exe')
    $output=Join-Path $repoRoot ('artifacts/npc-20261103/'+$Prefix+'-'+$case.Name)
    if(Test-Path -LiteralPath (Join-Path $output 'exit-code.txt')){throw 'Preserve completed measurements and choose a new prefix.'}
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $arguments=@('-screen-width','1536','-screen-height','864','-screen-fullscreen','0',
        '--worldgen-npc-check',('"'+$fixture+'"'),'--worldgen-npc-context','wet',
        '--worldgen-npc-hour','11','--worldgen-npc-perf','--worldgen-output',('"'+$output+'"'),
        '-logFile',('"'+$output+'/player.log"'))
    $process=Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output ($case.Name+' '+$case.Slot+' PID '+$process.Id)
    $process.WaitForExit()
    [IO.File]::WriteAllText((Join-Path $output 'exit-code.txt'),[string]$process.ExitCode)
    if($process.ExitCode -ne 0){throw ('Performance check failed; evidence retained: '+$output)}
    Write-Output ($case.Name+' EXIT '+$process.ExitCode)
}
