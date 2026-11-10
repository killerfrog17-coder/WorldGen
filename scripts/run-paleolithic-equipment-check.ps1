param(
    [ValidateSet('slot-a','slot-b')][string]$Slot='slot-b',
    [ValidatePattern('^[a-z0-9-]+$')][string]$Name='clothing-native-v3'
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$player=Join-Path $repoRoot ('builds/'+$Slot+'/WorldGen.exe')
if(Test-Path -LiteralPath (Join-Path (Split-Path -Parent $player) 'build-incomplete.txt')){throw 'Player build is incomplete.'}
if(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'"){throw 'Leave the running game untouched; run this check after it exits.'}
$output=Join-Path $repoRoot ('artifacts/paleolithic-night-20261110/'+$Name)
if(Test-Path -LiteralPath (Join-Path $output 'player.log')){throw 'Choose a new evidence name.'}
New-Item -ItemType Directory -Force -Path $output | Out-Null
Add-Type -AssemblyName System.Windows.Forms
$screen=[System.Windows.Forms.Screen]::AllScreens|Sort-Object {$_.Bounds.X}|Select-Object -First 1
$monitor=[regex]::Match($screen.DeviceName,'\d+$').Value
$fixture=Join-Path $repoRoot 'unity/WorldGen.Unity/Assets/StreamingAssets/world/Earth.world'
$arguments=@('-monitor',$monitor,'-screen-width','1280','-screen-height','800','-screen-fullscreen','0',
    '--worldgen-portable-craft-check',('"'+$fixture+'"'),'--worldgen-clothing-review',
    '--worldgen-output',('"'+$output+'"'),'--worldgen-library-root',('"'+$output+'/save-library"'),'-logFile',('"'+$output+'/player.log"'))
# These checks include the actual inventory OnGUI output. A hidden batch-mode
# swap chain produced black captures despite valid native skin geometry.
$process=Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Normal -PassThru
Write-Output ('Paleolithic clothing review PID '+$process.Id+' on leftmost monitor')
$process.WaitForExit()
[IO.File]::WriteAllText((Join-Path $output 'exit-code.txt'),[string]$process.ExitCode)
if($process.ExitCode -ne 0){throw ('Native clothing check failed: '+$output)}
if(!(Test-Path -LiteralPath (Join-Path $output 'checks.txt'))){throw 'Player exited without finishing the checks.'}
Write-Output ('PASS '+$output)
