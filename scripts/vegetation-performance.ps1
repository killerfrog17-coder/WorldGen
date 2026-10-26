param(
    [string]$PlayerFolder='player-vegetation-performance',
    [string]$Snapshot='world/.trash/6322ddf05ff74f619af02d3b88c8426c-943147b5a5e44d04a492a0a4875581a9/snapshot.world',
    [string]$Output='artifacts/performance-review',
    [string]$Sites='plains,forest-flat,forest-hills,mountains,desert,tundra',
    [string]$Cases='',
    [double]$Seconds=6,
    [double]$ViewDistance=60000,
    [int]$Width=1920,[int]$Height=1080,
    [int]$TimeoutMinutes=45,
    [switch]$PresentMon,
    [switch]$StutterTrace,
    [switch]$Original
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$player=Join-Path $repo "artifacts/$PlayerFolder/WorldGen.exe"
$inputFile=[IO.Path]::GetFullPath((Join-Path $repo $Snapshot))
$destination=[IO.Path]::GetFullPath((Join-Path $repo $Output))
if(!$destination.StartsWith((Join-Path $repo 'artifacts/'),[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must stay inside artifacts'}
if(Test-Path -LiteralPath (Join-Path $destination 'summary.csv')){throw 'Choose a new evidence directory; existing results are not overwritten'}
if(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'"){throw 'A game is already running; leave it untouched and benchmark later'}
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$before=(Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash
$arguments=@('-force-d3d11','-screen-fullscreen','0','-screen-width',"$Width",'-screen-height',"$Height",'--worldgen-performance',('"'+$inputFile+'"'),'--worldgen-output',('"'+$destination+'"'),'--worldgen-perf-width',"$Width",'--worldgen-perf-height',"$Height",'--worldgen-perf-sites',('"'+$Sites+'"'),'--worldgen-perf-seconds',$Seconds.ToString([Globalization.CultureInfo]::InvariantCulture),'-logFile',('"'+(Join-Path $destination 'player.log')+'"'))
if($Original){$arguments+='--worldgen-perf-original'}
if($StutterTrace){$arguments+='--worldgen-stutter-trace'}
if($Cases){$arguments+=@('--worldgen-perf-cases',('"'+$Cases+'"'))}
$arguments+=@('--worldgen-perf-view',$ViewDistance.ToString([Globalization.CultureInfo]::InvariantCulture))
$run=$null;$capture=$null
try {
    $run=Start-Process -FilePath $player -ArgumentList $arguments -WorkingDirectory $repo -WindowStyle Hidden -PassThru
    if($PresentMon){
        $capture=Start-Process -FilePath (Join-Path $repo 'artifacts/fps-diagnostic/PresentMon-2.6.0-x64.exe') -ArgumentList @('--process_id',"$($run.Id)",'--output_file',('"'+(Join-Path $destination 'presentmon.csv')+'"'),'--qpc_time','--no_console_stats','--terminate_on_proc_exit','--session_name',"WorldGenPerf$($run.Id)") -WindowStyle Hidden -PassThru
    }
    $deadline=[DateTime]::UtcNow.AddMinutes($TimeoutMinutes);$last=''
    while(!$run.WaitForExit(10000)){
        if([DateTime]::UtcNow -gt $deadline){throw 'Benchmark exceeded timeout'}
        $summary=Join-Path $destination 'summary.csv'
        if(Test-Path -LiteralPath $summary){$row=Get-Content -LiteralPath $summary -Tail 1;if($row -ne $last){Write-Output $row;$last=$row}}
    }
    if($run.ExitCode -ne 0){throw "Benchmark exited $($run.ExitCode); inspect player.log"}
    if(!(Test-Path -LiteralPath (Join-Path $destination 'complete.txt'))){throw 'No completion marker'}
    if((Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash -ne $before){throw 'Input snapshot changed'}
    $before | Out-File -LiteralPath (Join-Path $destination 'input-sha256.txt') -Encoding ascii
    Write-Output "Completed, input unchanged: $destination"
} finally {
    # Only processes started by this invocation, never an owner's player.
    if($run -and !$run.HasExited){Stop-Process -Id $run.Id}
    if($capture -and !$capture.HasExited){if(!$capture.WaitForExit(10000)){Stop-Process -Id $capture.Id}}
}
