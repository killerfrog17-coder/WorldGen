param([string]$PlayerFolder='',[string]$OutputName='',[switch]$Water,[switch]$Firefly,[switch]$Woodwork,[switch]$SaveSlots,[switch]$Nature)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
if(!$OutputName){$OutputName='item-use-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')}
$testOutput=Join-Path $repoRoot ('artifacts/overnight-items-20261028/'+$OutputName)
if(Test-Path -LiteralPath (Join-Path $testOutput 'player.log')){throw 'Choose a new output name; existing check evidence is not overwritten.'}
New-Item -ItemType Directory -Force -Path $testOutput | Out-Null
$existing=Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'"
if($existing){throw 'A WorldGen player is already running. Leave it untouched and run this check later.'}
$playerPath=if($PlayerFolder){Join-Path $repoRoot ('artifacts/'+$PlayerFolder+'/WorldGen.exe')}else{(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $repoRoot 'WorldGen.exe - Shortcut.lnk')).TargetPath}
if(!(Test-Path -LiteralPath $playerPath)){throw 'Requested player is missing; build it first or select an existing checkpoint.'}
if(!(Test-Path -LiteralPath (Join-Path (Split-Path -Parent $playerPath) 'build-inputs.json'))){throw 'Player packaging is incomplete: wait for the build script to finish.'}
$arguments=@(
    '-batchmode','-screen-width','1280','-screen-height','800','-screen-fullscreen','0',
    '--worldgen-item-use-check',('"'+$repoRoot+'/unity/WorldGen.Unity/Assets/StreamingAssets/world/Earth.world"'),
    '--worldgen-output',('"'+$testOutput+'"'),
    '--worldgen-library-root',('"'+$repoRoot+'/artifacts/death-update/isolated-library"'),
    '-logFile',('"'+$testOutput+'/player.log"'))
if($Water){$arguments+='--worldgen-item-water-check'}
if($Firefly){$arguments+='--worldgen-firefly-check'}
if($Woodwork){$arguments+='--worldgen-woodwork-check'}
if($SaveSlots){$arguments+='--worldgen-save-slots-check'}
if($Nature){$arguments+='--worldgen-nature-check'}
$run=Start-Process -FilePath $playerPath -WindowStyle Hidden -PassThru -ArgumentList $arguments
Write-Output ('Item-use worker PID '+$run.Id+'; '+$testOutput+'/player.log')
