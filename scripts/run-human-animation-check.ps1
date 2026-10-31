param([ValidateSet('Kimodo','Legacy','Items','Water')][string]$Mode='Kimodo',[string]$PlayerFolder='player-human-kimodo',[Parameter(Mandatory=$true)][string]$OutputName)
$ErrorActionPreference='Stop'
$kimodoRepo=Split-Path -Parent $PSScriptRoot
$kimodoTask=Join-Path $kimodoRepo 'artifacts/kimodo-human-bank'
$kimodoOutput=Join-Path $kimodoTask $OutputName
if(Test-Path -LiteralPath (Join-Path $kimodoOutput 'player.log')){throw 'Choose a new evidence output name.'}
if(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'"){throw 'A WorldGen player is already running; leave it untouched.'}
$kimodoPlayer=Join-Path $kimodoRepo ('artifacts/'+$PlayerFolder+'/WorldGen.exe')
if(!(Test-Path -LiteralPath (Join-Path (Split-Path -Parent $kimodoPlayer) 'build-inputs.json'))){throw 'Player packaging is not complete.'}
New-Item -ItemType Directory -Force $kimodoOutput | Out-Null
$kimodoArgs=@('-batchmode','-force-d3d11','-screen-width','1280','-screen-height','800','--worldgen-item-use-check',('"'+$kimodoRepo+'/unity/WorldGen.Unity/Assets/StreamingAssets/world/Earth.world"'),'--worldgen-output',('"'+$kimodoOutput+'"'),'--worldgen-library-root',('"'+$kimodoTask+'/native-library"'),'-logFile',('"'+$kimodoOutput+'/player.log"'))
if($Mode -eq 'Kimodo'){$kimodoArgs+='--worldgen-kimodo-human-check'}
if($Mode -eq 'Legacy'){$kimodoArgs+='--worldgen-first-person-check'}
if($Mode -eq 'Water'){$kimodoArgs+='--worldgen-water-update-check'}
$kimodoRun=Start-Process -FilePath $kimodoPlayer -ArgumentList $kimodoArgs -WindowStyle Hidden -PassThru
[ordered]@{pid=$kimodoRun.Id;mode=$Mode;output=$kimodoOutput;player=$kimodoPlayer}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $kimodoTask 'owned-native-process.json') -Encoding UTF8
Write-Output ('Owned '+$Mode+' check PID '+$kimodoRun.Id)
$kimodoRun.WaitForExit()
[ordered]@{pid=$kimodoRun.Id;mode=$Mode;exit_code=$kimodoRun.ExitCode;finished_utc=[DateTime]::UtcNow.ToString('o');output=$kimodoOutput}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $kimodoOutput 'result.json') -Encoding UTF8
if($kimodoRun.ExitCode -ne 0){throw ('Native check failed: '+$kimodoOutput)}
if(Select-String -LiteralPath (Join-Path $kimodoOutput 'player.log') -Pattern 'NullReferenceException|InvalidOperationException|ArgumentException|Shader error|error CS' -Quiet){throw 'Native log contains runtime errors.'}
Get-Content -LiteralPath (Join-Path $kimodoOutput 'checks.txt')
