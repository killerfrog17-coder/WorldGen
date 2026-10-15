param([Parameter(Mandatory=$true)][string]$Folder)
$ErrorActionPreference='Stop'
$folderRoot=(Resolve-Path -LiteralPath $Folder).Path
$timeline=Get-Content -LiteralPath (Join-Path $folderRoot 'timeline.json') -Raw | ConvertFrom-Json
$events=Get-Content -LiteralPath (Join-Path $folderRoot 'events.json') -Raw | ConvertFrom-Json
$towns=Get-Content -LiteralPath (Join-Path $folderRoot 'towns.json') -Raw | ConvertFrom-Json
$accounts=Get-Content -LiteralPath (Join-Path $folderRoot 'accounts.json') -Raw | ConvertFrom-Json
$last=$timeline[-1]
$gunpowder=($events | Where-Object {$_.Kind -eq 'technology' -and $_.Text -match 'Gunpowder'} | Select-Object -First 1).Year
$ages=@($towns | Where-Object {$_.Population -gt 0} | ForEach-Object {$last.Year-$_.FoundedYear} | Sort-Object)
$stateAges=@($accounts.States | Where-Object {$_.EndedYear -lt 0} | ForEach-Object {$last.Year-$_.FoundedYear} | Sort-Object)
[PSCustomObject]@{
    Year=$last.Year;FirstState=$last.FirstState;FirstMaximumTechnology=$last.MaxTechYear
    FirstGunpowder=$gunpowder
    StateCulturesBeforeGunpowder=@($events | Where-Object {$_.Kind -in 'state','secession' -and $_.Year -le $gunpowder} | Select-Object -ExpandProperty CultureId -Unique).Count
    LiveTowns=$ages.Count;AbandonedSites=@($accounts.LivingCities | Where-Object {$_.AbandonedYear -ge 0}).Count
    MedianLiveTownAge=if($ages.Count){$ages[[int][Math]::Floor($ages.Count/2)]}else{0}
    OldestLiveTownAge=if($ages.Count){$ages[-1]}else{0}
    LiveStates=$stateAges.Count
    MedianLiveStateAge=if($stateAges.Count){$stateAges[[int][Math]::Floor($stateAges.Count/2)]}else{0}
    IndependentOrigins=@($events | Where-Object {$_.Kind -eq 'independent-institutions'}).Count
    IndependentOriginsInFirst2000Years=@($events | Where-Object {$_.Kind -eq 'independent-institutions' -and $_.Year -le $last.FirstState+2000}).Count
    TransmittedFoundations=@($events | Where-Object {$_.Kind -eq 'institutional-diffusion'}).Count
    RegimeChanges=@($events | Where-Object {$_.Kind -eq 'regime-change'}).Count
    CourtDissolutions=@($events | Where-Object {$_.Kind -eq 'collapse' -and $_.Text -match 'Fiscal strain'}).Count
    SettlementConquests=@($events | Where-Object {$_.Kind -eq 'conquest'}).Count
} | ConvertTo-Json
