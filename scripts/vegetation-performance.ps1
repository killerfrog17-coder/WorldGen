param(
    [ValidateSet('slot-a','slot-b')][string]$PlayerFolder='slot-b',
    [string]$Snapshot='world/.trash/6322ddf05ff74f619af02d3b88c8426c-943147b5a5e44d04a492a0a4875581a9/snapshot.world',
    [string]$Output='artifacts/performance-review',
    [string]$Sites='plains,forest-flat,forest-hills,mountains,desert,tundra',
    [string]$SitesFile='',
    [string]$Cases='',
    [double]$Seconds=6,
    [double]$ViewDistance=60000,
    [int]$PlantQuality=2,
    [int]$Width=1920,[int]$Height=1080,
    [int]$TimeoutMinutes=45,
    [switch]$PresentMon,
    [switch]$Presented,
    [switch]$StutterTrace,
    [switch]$SavedSettings,
    [switch]$GpuTelemetry,
    [switch]$Original
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$player=Join-Path $repo "builds/$PlayerFolder/WorldGen.exe"
if(Test-Path -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($player)) 'build-incomplete.txt')){throw 'The last build has not passed packaging and shader checks; do not benchmark it.'}
$inputFile=[IO.Path]::GetFullPath((Join-Path $repo $Snapshot))
$destination=[IO.Path]::GetFullPath((Join-Path $repo $Output))
if(!$destination.StartsWith((Join-Path $repo 'artifacts/'),[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must stay inside artifacts'}
if(Test-Path -LiteralPath (Join-Path $destination 'summary.csv')){throw 'Choose a new evidence directory; existing results are not overwritten'}
if(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'"){throw 'A game is already running; leave it untouched and benchmark later'}
if($PresentMon -and !(Test-Path -LiteralPath (Join-Path $repo 'tools/PresentMon/PresentMon.exe'))){throw 'PresentMon is missing. Restore the pinned standalone tool recorded in assets/verification/presentmon-tool.json before launching a benchmark.'}
$availableSites=@('plains','forest-flat','forest-hills','mountains','desert','tundra')
if($SitesFile){$siteValidationPath=[IO.Path]::GetFullPath((Join-Path $repo $SitesFile));$availableSites+=@((Get-Content -LiteralPath $siteValidationPath -Raw|ConvertFrom-Json).Sites|ForEach-Object {$_.Name})}
foreach($requestedSite in $Sites.Split(',')){if($requestedSite -notin $availableSites){throw "Unknown performance site: $requestedSite. Available: $($availableSites -join ', ')"}}
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$playerRoot=Split-Path -Parent $player
$buildStamp=Get-Content -LiteralPath (Join-Path $playerRoot 'build-inputs.json') -Raw | ConvertFrom-Json
[ordered]@{
    AssemblySha256=(Get-FileHash -LiteralPath (Join-Path $playerRoot 'WorldGen_Data/Managed/Assembly-CSharp.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    CoreSha256=(Get-FileHash -LiteralPath (Join-Path $playerRoot 'WorldGen_Data/Managed/WorldGen.Core.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    WorkerSha256=(Get-FileHash -LiteralPath (Join-Path $playerRoot 'TerrainWorker/WorldGen.Cli.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
    Build=$buildStamp
    RecordedUtc=[DateTime]::UtcNow.ToString('o')
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $destination 'package.json') -Encoding UTF8
$before=(Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash
$arguments=@('-force-d3d11','-screen-fullscreen','0','-screen-width',"$Width",'-screen-height',"$Height",'--worldgen-performance',('"'+$inputFile+'"'),'--worldgen-output',('"'+$destination+'"'),'--worldgen-perf-width',"$Width",'--worldgen-perf-height',"$Height",'--worldgen-perf-sites',('"'+$Sites+'"'),'--worldgen-perf-seconds',$Seconds.ToString([Globalization.CultureInfo]::InvariantCulture),'-logFile',('"'+(Join-Path $destination 'player.log')+'"'))
if($Original){$arguments+='--worldgen-perf-original'}
if($StutterTrace){$arguments+='--worldgen-stutter-trace'}
if($Presented){$arguments+='--worldgen-perf-presented'}
if($SavedSettings){$arguments+='--worldgen-perf-saved-settings'}
if($Cases){$arguments+=@('--worldgen-perf-cases',('"'+$Cases+'"'))}
if($SitesFile){$sitesInput=[IO.Path]::GetFullPath((Join-Path $repo $SitesFile));if(!(Test-Path -LiteralPath $sitesInput)){throw 'Performance sites file missing'};$arguments+=@('--worldgen-perf-sites-file',('"'+$sitesInput+'"'))}
$arguments+=@('--worldgen-perf-view',$ViewDistance.ToString([Globalization.CultureInfo]::InvariantCulture))
$arguments+=@('--worldgen-perf-plant-quality',"$PlantQuality")
$run=$null;$capture=$null;$telemetry=$null
try {
    if($GpuTelemetry){
        $telemetryTool=Get-Command nvidia-smi.exe -ErrorAction Stop
        $telemetry=Start-Process -FilePath $telemetryTool.Source -ArgumentList @('--query-gpu=timestamp,utilization.gpu,clocks.gr,clocks.mem,temperature.gpu,power.draw,memory.used','--format=csv','-l','1','-f',('"'+(Join-Path $destination 'gpu-telemetry.csv')+'"')) -WindowStyle Hidden -PassThru
    }
    # Presented mode is an interactive game window whose normal swap chain is
    # being measured; ordinary offscreen evidence remains a hidden helper.
    $playerWindowStyle=if($Presented){'Normal'}else{'Hidden'}
    $run=Start-Process -FilePath $player -ArgumentList $arguments -WorkingDirectory $repo -WindowStyle $playerWindowStyle -PassThru
    if($Presented){& (Join-Path $PSScriptRoot 'place-review-window.ps1') -ReviewProcessId $run.Id}
    if($PresentMon){
        $capture=Start-Process -FilePath (Join-Path $repo 'tools/PresentMon/PresentMon.exe') -ArgumentList @('--process_id',"$($run.Id)",'--output_file',('"'+(Join-Path $destination 'presentmon.csv')+'"'),'--qpc_time','--no_console_stats','--terminate_on_proc_exit','--session_name',"WorldGenPerf$($run.Id)") -RedirectStandardOutput (Join-Path $destination 'presentmon-output.txt') -RedirectStandardError (Join-Path $destination 'presentmon-error.txt') -WindowStyle Hidden -PassThru
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
    if($telemetry -and !$telemetry.HasExited){Stop-Process -Id $telemetry.Id}
}
