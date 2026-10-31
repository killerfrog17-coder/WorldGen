param(
    [ValidateSet('Appearance','Kimodo','Legacy')][string]$Mode='Appearance',
    [ValidateSet('slot-a','slot-b')][string]$Slot='slot-b',
    [Parameter(Mandatory=$true)][string]$OutputName
)
$ErrorActionPreference='Stop'
$appearanceRepo=Split-Path -Parent $PSScriptRoot
if($OutputName -notmatch '^[a-zA-Z0-9_-]+$'){throw 'OutputName must be a simple directory name.'}
if(Get-Process WorldGen -ErrorAction SilentlyContinue){throw 'A WorldGen process is already running; leave it untouched.'}
$appearanceOutput=Join-Path $appearanceRepo ('artifacts/human-appearance-integration/'+$OutputName)
if(Test-Path -LiteralPath $appearanceOutput){throw 'Use a fresh evidence output name.'}
$appearancePlayer=Join-Path $appearanceRepo ('builds/'+$Slot+'/WorldGen.exe')
$appearanceStamp=Get-Content -Raw -LiteralPath (Join-Path (Split-Path -Parent $appearancePlayer) 'build-inputs.json') | ConvertFrom-Json
if($appearanceStamp.Backend -ne 'IL2CPP'){throw 'Native IL2CPP is required.'}
New-Item -ItemType Directory -Path $appearanceOutput | Out-Null
$appearanceArgs=@('-batchmode','-force-d3d11','-screen-width','1280','-screen-height','800','--worldgen-output',('"'+$appearanceOutput+'"'),'-logFile',('"'+$appearanceOutput+'/player.log"'))
if($Mode -eq 'Appearance'){$appearanceArgs+='--worldgen-human-appearance-check'}
else{
    $appearanceArgs+=@('--worldgen-item-use-check',('"'+$appearanceRepo+'/unity/WorldGen.Unity/Assets/StreamingAssets/world/Earth.world"'),'--worldgen-library-root',('"'+$appearanceOutput+'/library"'))
    $appearanceArgs+=if($Mode -eq 'Kimodo'){'--worldgen-kimodo-human-check'}else{'--worldgen-first-person-check'}
}
$appearanceRun=Start-Process -FilePath $appearancePlayer -ArgumentList $appearanceArgs -WorkingDirectory $appearanceRepo -WindowStyle Hidden -PassThru
Write-Output ('Owned '+$Mode+' check PID '+$appearanceRun.Id)
$appearanceRun.WaitForExit()
$appearanceChecks=Join-Path $appearanceOutput 'checks.txt'
$appearanceLines=if(Test-Path -LiteralPath $appearanceChecks){@(Get-Content -LiteralPath $appearanceChecks)}else{@()}
$appearanceErrors=Select-String -LiteralPath (Join-Path $appearanceOutput 'player.log') -Pattern 'Exception|Shader error|error CS|Assertion failed' -Quiet
$appearancePassed=$appearanceRun.ExitCode -eq 0 -and $appearanceLines.Count -gt 0 -and !($appearanceLines | Where-Object {$_ -notmatch '^PASS '}) -and !$appearanceErrors
[ordered]@{mode=$Mode;slot=$Slot;exit_code=$appearanceRun.ExitCode;checks=$appearanceLines.Count;passed=$appearancePassed;built_utc=$appearanceStamp.BuiltUtc;game_assembly_sha256=(Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $appearancePlayer) 'GameAssembly.dll')).Hash.ToLowerInvariant();finished_utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $appearanceOutput 'result.json') -Encoding UTF8
if(!$appearancePassed){throw ('Native '+$Mode+' check failed: '+$appearanceOutput)}
Write-Output ('PASS '+$appearanceLines.Count+' '+$Mode+' checks')
