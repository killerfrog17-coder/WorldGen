param(
    [ValidateSet('slot-a','slot-b')][string]$Slot='slot-a',
    [ValidatePattern('^[a-z0-9-]+$')][string]$Name,
    [ValidateSet('SandSea','WoodedSwamp','GlacialTrough','TemperateForest','Grassland','Canyon')][string]$Kind='TemperateForest',
    [string]$World='artifacts/terrain-surfaces-20261107/playable.world',
    [switch]$Legacy,
    [switch]$Travel,
    [switch]$Motion,
    [switch]$LegacyPlan,
    [switch]$FastFlight,
    [ValidateRange(0,3)][int]$Effort=1,
    [switch]$LongFlight
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$player=Join-Path $repoRoot ('builds/'+$Slot+'/WorldGen.exe')
if(Get-CimInstance Win32_Process | Where-Object {$_.Name -ieq 'WorldGen.exe'}){throw 'Leave the running game untouched.'}
if(Test-Path -LiteralPath (Join-Path $repoRoot ('builds/'+$Slot+'/build-incomplete.txt'))){throw 'Incomplete build.'}
$output=Join-Path $repoRoot ('artifacts/chunk-loading-20261109/'+$Name)
if(Test-Path -LiteralPath $output){throw 'Choose a fresh measurement name.'}
New-Item -ItemType Directory -Path $output | Out-Null
$fixture=(Resolve-Path -LiteralPath (Join-Path $repoRoot $World)).Path
$arguments=@('-batchmode','-screen-width','1536','-screen-height','864','-screen-fullscreen','0',
    '--worldgen-landscape-check',('"'+$fixture+'"'),'--worldgen-landscape-kind',$Kind,
    '--worldgen-landscape-quality','2','--worldgen-chunk-loading-check','--worldgen-no-animals',
    '--worldgen-output',('"'+$output+'"'),'-logFile',('"'+$output+'/player.log"'))
if($Legacy){$arguments+='--worldgen-loader-legacy'}
if($Travel){$arguments+='--worldgen-chunk-travel'}
if($Motion){$arguments+='--worldgen-chunk-motion'}
if($LegacyPlan){$arguments+='--worldgen-plan-bootstrap-legacy'}
if($FastFlight){$arguments+='--worldgen-chunk-fast-flight'}
if($LongFlight){$arguments+='--worldgen-chunk-long-flight'}
$arguments+=@('--worldgen-chunk-effort',[string]$Effort)
$process=Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output ($Name+' PID '+$process.Id)
$process.WaitForExit()
[IO.File]::WriteAllText((Join-Path $output 'exit-code.txt'),[string]$process.ExitCode)
if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $output 'complete.txt'))){throw ('Chunk check failed: '+$output)}
Get-Content -LiteralPath (Join-Path $output 'chunk-loading.txt')
