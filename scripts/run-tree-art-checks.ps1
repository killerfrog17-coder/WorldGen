param(
    [ValidateSet('slot-a','slot-b')][string]$Slot='slot-a',
    [ValidatePattern('^[a-z0-9-]+$')][string]$Prefix='trees-canopy'
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$player=Join-Path $repo ('builds/'+$Slot+'/WorldGen.exe')
if(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'" | Where-Object {$_.ExecutablePath -eq $player}){throw 'This player slot is open; leave that session untouched.'}
Add-Type -AssemblyName System.Windows.Forms
$treeReviewMonitor=[System.Windows.Forms.Screen]::AllScreens|Sort-Object {$_.Bounds.X} -Descending|Select-Object -First 1
$treeReviewMonitorIndex=[regex]::Match($treeReviewMonitor.DeviceName,'\d+$').Value
if(!$treeReviewMonitorIndex){throw 'Cannot identify rightmost review monitor'}
$assembly=Join-Path $repo ('builds/'+$Slot+'/WorldGen_Data/Managed/Assembly-CSharp.dll')
$core=Join-Path $repo ('builds/'+$Slot+'/WorldGen_Data/Managed/WorldGen.Core.dll')
$worker=Join-Path $repo ('builds/'+$Slot+'/TerrainWorker/WorldGen.Cli.exe')
foreach($kind in @('tree','forest')){
    $output=Join-Path $repo ('artifacts/tree-20261103/'+$Prefix+'-'+$kind+'-shader')
    if(Test-Path -LiteralPath (Join-Path $output 'package.json')){throw 'Preserve previous evidence and use another prefix.'}
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $receipt=[ordered]@{Slot=$Slot;Built=(Get-Content -Raw -LiteralPath (Join-Path $repo ('builds/'+$Slot+'/build-inputs.json'))|ConvertFrom-Json);Assembly=(Get-FileHash -LiteralPath $assembly).Hash;Core=(Get-FileHash -LiteralPath $core).Hash;Worker=(Get-FileHash -LiteralPath $worker).Hash}
    $receipt|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $output 'package.json')
    $arguments=@('-batchmode','-monitor',$treeReviewMonitorIndex,'-screen-width','1280','-screen-height','720','-screen-fullscreen','0',('--worldgen-'+$kind+'-shader-check'),'--worldgen-output',('"'+$output+'"'),'-logFile',('"'+$output+'/player.log"'))
    $run=Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output ($kind+' shader PID '+$run.Id);$run.WaitForExit()
    [IO.File]::WriteAllText((Join-Path $output 'exit-code.txt'),[string]$run.ExitCode)
    if($run.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $output 'PASS.txt'))){throw ('Native tree art check failed: '+$output)}
    if((Get-FileHash -LiteralPath $assembly).Hash -ne $receipt.Assembly){throw 'Player changed during check.'}
    Write-Output ($kind+' shader PASS')
}
