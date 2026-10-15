param(
    [Parameter(Mandatory=$true)][string]$World,
    [string]$PlayerFolder='player-history-16-performance',
    [int]$Years=1000,
    [string]$Output=''
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$inputWorld=(Resolve-Path -LiteralPath $World).Path
$player=Join-Path $repoRoot ('artifacts/'+$PlayerFolder+'/WorldGen.exe')
if(!(Test-Path -LiteralPath $player)){throw 'Build the Unity player first.'}
if($Years -lt 1000){throw 'Acceptance requires at least 1000 simulated years.'}
if(!$Output){$Output=Join-Path $repoRoot ('artifacts/history-performance/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$outputRoot=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$inputHash=(Get-FileHash -LiteralPath $inputWorld).Hash
$arguments=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','--worldgen-history-playback',('"'+$inputWorld+'"'),'--worldgen-history-tail',"$Years",'--worldgen-duration',"$([Math]::Max(600,$Years))",'--worldgen-output',('"'+$outputRoot+'"'),'-logFile',('"'+(Join-Path $outputRoot 'player.log')+'"'))
$process=Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
$started=Get-Date
while(!$process.WaitForExit(1000)){
    if(((Get-Date)-$started).TotalSeconds -gt [Math]::Max(1200,$Years+600)){
        # This PID belongs to this diagnostic, never an existing user's app.
        Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
        throw 'Owned performance test exceeded its timeout.'
    }
}
if((Get-FileHash -LiteralPath $inputWorld).Hash -ne $inputHash){throw 'The benchmark input changed.'}
$reportPath=Join-Path $outputRoot 'playback.json'
if(!(Test-Path -LiteralPath $reportPath)){throw "Player produced no acceptance report. Inspect $outputRoot/player.log"}
$report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$report
if($process.ExitCode -ne 0 -or !$report.AcceptancePassed){throw "Performance acceptance failed. Evidence: $reportPath"}
Write-Host "PASS: $($report.SimulatedYears) years; $($report.AchievedYearsPerSecond) years/s; worst year $($report.MaximumYearMilliseconds) ms."
