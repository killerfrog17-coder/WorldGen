param(
    [ValidateRange(5000,2560000)][int]$Detail=20000,
    [ValidateRange(10000,250000)][int]$Years=200000,
    [ValidateRange(500,1000)][int]$AfterMaxTech=750,
    [ValidateRange(1,1000)][int]$SampleYears=250,
    [ValidateRange(25,5000)][int]$MapInterval=2500,
    [switch]$Screenshots,
    [switch]$FinalMapOnly,
    [string]$OutputRoot="artifacts"
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    # Fresh directory: never overwrite a user's saved worlds or prior audit evidence.
    $auditBase=[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($OutputRoot)){$OutputRoot}else{Join-Path $repoRoot $OutputRoot}))
    New-Item -ItemType Directory -Force $auditBase | Out-Null
    $runFolder=Join-Path $auditBase ('history-audit-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $runFolder | Out-Null
    dotnet build tests/WorldGen.Core.Tests -c Release
    if($LASTEXITCODE -ne 0){throw 'Audit build failed'}
    $testDll=Join-Path $repoRoot 'tests/WorldGen.Core.Tests/bin/Release/net10.0/WorldGen.Core.Tests.dll'
    foreach($case in @(@('Earth',73421),@('Continents plus',17),@('Archipelago',42))) {
        $label=($case[0] -replace ' ','-')+'-'+$case[1]
        $geography=Join-Path $runFolder ($label+'.world')
        $output=Join-Path $runFolder $label
        dotnet $testDll --history-geography $case[0] $case[1] $Detail $geography
        if($LASTEXITCODE -ne 0){throw "Geography failed: $label"}
        $auditArguments=@($testDll,'--history-audit',$geography,$output,$Years,21,'--after-max-tech',$AfterMaxTech,'--sample-years',$SampleYears,'--map-interval',$MapInterval)
        if($FinalMapOnly){$auditArguments+='--final-map-only'}
        dotnet @auditArguments
        if($LASTEXITCODE -ne 0){throw "History failed: $label"}
        if($Screenshots){$endYear=(Import-Csv (Join-Path $output 'timeline.csv') | Select-Object -Last 1).year;node scripts/history-snapshot-images.mjs $output $endYear;if($LASTEXITCODE -ne 0){throw "Screenshot failed: $label"};if(!$FinalMapOnly){node scripts/history-era-images.mjs $output;if($LASTEXITCODE -ne 0){throw "Era storyboard failed: $label"}}}
    }
    Write-Host "Audit complete: $runFolder"
    Write-Host 'Review timeline.csv, dated maps/site CSVs, sea-routes.csv and chronicle.txt. These are diagnostics, not calibrated historical expectations.'
} finally {Pop-Location}
