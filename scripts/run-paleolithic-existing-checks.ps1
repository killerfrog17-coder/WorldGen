param(
 [ValidateSet('slot-a','slot-b')][string]$Slot='slot-b',
 [ValidateSet('Items','Woodwork','Blueprints','Camp','Nature')][string[]]$Cases=@('Items','Woodwork','Blueprints','Camp'),
 [Parameter(Mandatory=$true)][ValidatePattern('^[a-z0-9-]+$')][string]$Prefix
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'"){throw 'Leave the running game untouched.'}
if(Test-Path -LiteralPath (Join-Path $repo ('builds/'+$Slot+'/build-incomplete.txt'))){throw 'Build incomplete.'}
Add-Type -AssemblyName System.Windows.Forms
$screen=[System.Windows.Forms.Screen]::AllScreens|Sort-Object {$_.Bounds.X}|Select-Object -First 1
$monitor=[regex]::Match($screen.DeviceName,'\d+$').Value
foreach($case in $Cases){
 $output=Join-Path $repo ('artifacts/paleolithic-night-20261110/'+$Prefix+'-'+$case.ToLowerInvariant())
 if(Test-Path -LiteralPath $output){throw 'Choose a new evidence prefix.'}
 New-Item -ItemType Directory -Path $output|Out-Null
 $arguments=@('-monitor',$monitor,'-screen-width','1280','-screen-height','800','-screen-fullscreen','0','--worldgen-item-use-check',('"'+$repo+'/unity/WorldGen.Unity/Assets/StreamingAssets/world/Earth.world"'),'--worldgen-output',('"'+$output+'"'),'--worldgen-library-root',('"'+$output+'/library"'),'-logFile',('"'+$output+'/player.log"'))
 if($case -eq 'Woodwork'){$arguments+='--worldgen-woodwork-check'}
 if($case -eq 'Blueprints'){$arguments+='--worldgen-blueprint-check'}
 if($case -eq 'Camp'){$arguments+='--worldgen-camp-check'}
 if($case -eq 'Nature'){$arguments+='--worldgen-nature-check'}
 $process=Start-Process -FilePath (Join-Path $repo ('builds/'+$Slot+'/WorldGen.exe')) -ArgumentList $arguments -WindowStyle Normal -PassThru
 Write-Output ($case+' check PID '+$process.Id)
 if(!$process.WaitForExit(600000)){$process.Kill();throw 'Owned functional check exceeded ten minutes.'}
 [IO.File]::WriteAllText((Join-Path $output 'exit-code.txt'),[string]$process.ExitCode)
 if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $output 'checks.txt'))){throw ('Failed '+$case+': '+$output)}
 Write-Output ('PASS '+$case+' '+(Get-Content (Join-Path $output 'checks.txt')).Count+' checks')
}
