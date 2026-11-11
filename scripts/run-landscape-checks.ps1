param(
    [ValidateSet('slot-a','slot-b')][string]$Slot='slot-b',
    [ValidatePattern('^[a-z0-9-]+$')][string]$Prefix='regions-first',
    [string]$World='artifacts/npc-20261103/current-190k.world',
    [string[]]$Kinds=@('pond','SandSea','GravelDesert','ClayPan','SaltFlat','FreshwaterMarsh','WoodedSwamp','Peatland','LimestoneUpland','GlacialLowland'),
    [switch]$TreeAlphaReview,
    [switch]$GrassReview,
    [switch]$TreeShadowReview,
    [switch]$BarkReview,
    [switch]$WaterShadingReview,
    [switch]$CliffReview,
    [switch]$Presented,
    [ValidateSet('Windowed','Borderless')][string]$WindowMode='Windowed',
    [switch]$KeepOpen,
    [ValidateSet('Leftmost','Rightmost')][string]$ReviewMonitor='Rightmost',
    [ValidateRange(0,4)][int]$PlantQuality=3,
    [ValidateRange(-1,4)][int]$TerrainQuality=-1,
    [string[]]$Captures=@(),
    [int]$SourceCell=-1
)
$ErrorActionPreference='Stop'
if($KeepOpen -and (!$Presented -or $Kinds.Count -ne 1)){throw 'KeepOpen requires one presented review kind.'}
if($WindowMode -eq 'Borderless' -and !$Presented){throw 'Borderless review requires a presented player'}
$repoRoot=Split-Path -Parent $PSScriptRoot
$player=Join-Path $repoRoot ('builds/'+$Slot+'/WorldGen.exe')
if(Test-Path -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($player)) 'build-incomplete.txt')){throw 'The last build has not passed packaging and shader checks; do not review it.'}
$fixture=Join-Path $repoRoot $World
Add-Type -AssemblyName System.Windows.Forms
$reviewScreen=[System.Windows.Forms.Screen]::AllScreens|Sort-Object {$_.Bounds.X} -Descending:($ReviewMonitor -eq 'Rightmost')|Select-Object -First 1
$reviewMonitorIndex=[regex]::Match($reviewScreen.DeviceName,'\d+$').Value
if(!$reviewMonitorIndex){throw 'Cannot identify the selected review monitor'}
if(!(Test-Path -LiteralPath $fixture)){throw 'Generate the disposable world fixture first.'}
if(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'" | Where-Object {$_.ExecutablePath -eq $player}){throw 'This player slot is already running; leave that session untouched.'}
foreach($kind in $Kinds){
    if($kind -notmatch '^[a-zA-Z]+$'){throw 'Invalid landscape kind'}
    $output=Join-Path $repoRoot ('artifacts/landscape-20261103/'+$Prefix+'-'+$kind.ToLowerInvariant())
    if(Test-Path -LiteralPath (Join-Path $output 'exit-code.txt')){throw 'Preserve completed evidence and choose a new prefix.'}
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    # These render into explicit textures, so no interactive window is needed.
    $reviewWidth=if($WindowMode -eq 'Borderless'){$reviewScreen.Bounds.Width}else{1536}
    $reviewHeight=if($WindowMode -eq 'Borderless'){$reviewScreen.Bounds.Height}else{864}
    $arguments=@('-monitor',$reviewMonitorIndex,'-screen-width',[string]$reviewWidth,'-screen-height',[string]$reviewHeight,'-screen-fullscreen','0','--worldgen-landscape-check',('"'+$fixture+'"'),'--worldgen-landscape-kind',$kind,'--worldgen-output',('"'+$output+'"'),'-logFile',('"'+$output+'/player.log"'))
    if($Presented){$arguments+='--worldgen-landscape-presented'}else{$arguments+='-batchmode'}
    if($WindowMode -eq 'Borderless'){$arguments+=@('--worldgen-landscape-window-mode','Borderless')}
    if($KeepOpen){$arguments+='--worldgen-landscape-keep-open'}
    if($TreeAlphaReview){$arguments+='--worldgen-tree-alpha-review'}
    if($GrassReview){$arguments+='--worldgen-grass-review'}
    if($TreeShadowReview){$arguments+='--worldgen-tree-shadow-review'}
    if($BarkReview){$arguments+='--worldgen-bark-review'}
    if($WaterShadingReview){$arguments+='--worldgen-water-shading-review'}
    if($CliffReview){$arguments+='--worldgen-cliff-review'}
    $arguments+=@('--worldgen-landscape-quality',[string]$PlantQuality)
    if($TerrainQuality -ge 0){$arguments+=@('--worldgen-chunk-quality',[string]$TerrainQuality)}
    if($SourceCell -ge 0){$arguments+=@('--worldgen-landscape-source',[string]$SourceCell)}
    if($Captures.Count -gt 0){foreach($capture in $Captures){if($capture -notmatch '^[a-z0-9-]+$'){throw 'Invalid capture name'}};$arguments+=@('--worldgen-landscape-captures',($Captures -join ','))}
    $style=if($Presented){'Normal'}else{'Hidden'}
    $process=Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle $style -PassThru
    if($Presented){& (Join-Path $PSScriptRoot 'place-review-window.ps1') -ReviewProcessId $process.Id -Monitor $ReviewMonitor}
    Write-Output ($kind+' PID '+$process.Id);$process.WaitForExit()
    [IO.File]::WriteAllText((Join-Path $output 'exit-code.txt'),[string]$process.ExitCode)
    Write-Output ($kind+' EXIT '+$process.ExitCode)
    if($process.ExitCode -ne 0 -or (Test-Path -LiteralPath (Join-Path $output 'failure.txt'))){throw ('Landscape check failed; evidence retained: '+$output)}
}
