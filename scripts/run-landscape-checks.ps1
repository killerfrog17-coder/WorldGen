param(
    [ValidateSet('slot-a','slot-b')][string]$Slot='slot-b',
    [ValidatePattern('^[a-z0-9-]+$')][string]$Prefix='regions-first',
    [string]$World='artifacts/npc-20261103/current-190k.world',
    [string[]]$Kinds=@('pond','SandSea','GravelDesert','ClayPan','SaltFlat','FreshwaterMarsh','WoodedSwamp','Peatland','LimestoneUpland','GlacialLowland'),
    [switch]$TreeAlphaReview,
    [switch]$GrassReview,
    [switch]$TreeShadowReview,
    [ValidateRange(0,4)][int]$PlantQuality=3
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$player=Join-Path $repoRoot ('builds/'+$Slot+'/WorldGen.exe')
if(Test-Path -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($player)) 'build-incomplete.txt')){throw 'The last build has not passed packaging and shader checks; do not review it.'}
$fixture=Join-Path $repoRoot $World
Add-Type -AssemblyName System.Windows.Forms
$reviewMonitor=[System.Windows.Forms.Screen]::AllScreens|Sort-Object {$_.Bounds.X} -Descending|Select-Object -First 1
$reviewMonitorIndex=[regex]::Match($reviewMonitor.DeviceName,'\d+$').Value
if(!$reviewMonitorIndex){throw 'Cannot identify the rightmost monitor'}
if(!(Test-Path -LiteralPath $fixture)){throw 'Generate the disposable world fixture first.'}
if(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'" | Where-Object {$_.ExecutablePath -eq $player}){throw 'This player slot is already running; leave that session untouched.'}
foreach($kind in $Kinds){
    if($kind -notmatch '^[a-zA-Z]+$'){throw 'Invalid landscape kind'}
    $output=Join-Path $repoRoot ('artifacts/landscape-20261103/'+$Prefix+'-'+$kind.ToLowerInvariant())
    if(Test-Path -LiteralPath (Join-Path $output 'exit-code.txt')){throw 'Preserve completed evidence and choose a new prefix.'}
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    # These render into explicit textures, so no interactive window is needed.
    $arguments=@('-batchmode','-monitor',$reviewMonitorIndex,'-screen-width','1536','-screen-height','864','-screen-fullscreen','0','--worldgen-landscape-check',('"'+$fixture+'"'),'--worldgen-landscape-kind',$kind,'--worldgen-output',('"'+$output+'"'),'-logFile',('"'+$output+'/player.log"'))
    if($TreeAlphaReview){$arguments+='--worldgen-tree-alpha-review'}
    if($GrassReview){$arguments+='--worldgen-grass-review'}
    if($TreeShadowReview){$arguments+='--worldgen-tree-shadow-review'}
    $arguments+=@('--worldgen-landscape-quality',[string]$PlantQuality)
    $process=Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output ($kind+' PID '+$process.Id);$process.WaitForExit()
    [IO.File]::WriteAllText((Join-Path $output 'exit-code.txt'),[string]$process.ExitCode)
    Write-Output ($kind+' EXIT '+$process.ExitCode)
    if($process.ExitCode -ne 0 -or (Test-Path -LiteralPath (Join-Path $output 'failure.txt'))){throw ('Landscape check failed; evidence retained: '+$output)}
}
