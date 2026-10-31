param([switch]$Apply)
$ErrorActionPreference='Stop'
$retentionRepo=Split-Path -Parent $PSScriptRoot
$retentionPlan=Get-Content -Raw -LiteralPath (Join-Path $retentionRepo 'artifacts/player-retention-20261031/plan.json') | ConvertFrom-Json
$retentionReport=[ordered]@{Applied=[bool]$Apply;Removed=@();Retained=@();BeforeFree=@{};AfterFree=@{}}
foreach($drive in @('C','D','E')){$retentionReport.BeforeFree[$drive]=(Get-PSDrive $drive).Free}
foreach($package in $retentionPlan.packages){
    if($package.keep){continue}
    $retentionPackage=[IO.Path]::GetFullPath($package.path).TrimEnd('\')
    if(!@($retentionPlan.roots | Where-Object {$retentionPackage.StartsWith([IO.Path]::GetFullPath($_).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)}).Count){throw 'Package outside audited roots'}
    foreach($keep in $retentionPlan.protected){if($retentionPackage -eq [IO.Path]::GetFullPath($keep).TrimEnd('\')){throw 'Protected package'}}
    $selected=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $retentionRepo 'WorldGen.exe - Shortcut.lnk')).TargetPath
    if($selected.StartsWith($retentionPackage+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Selected player changed; regenerate plan'}
    foreach($running in Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe' OR Name='WorldGen.Cli.exe'"){if($running.ExecutablePath -and $running.ExecutablePath.StartsWith($retentionPackage+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Running player protected'}}
    foreach($target in $package.targets){
        $retentionTarget=[IO.Path]::GetFullPath($target)
        if([IO.Path]::GetDirectoryName($retentionTarget) -ne $retentionPackage){throw 'Only audited immediate package children allowed'}
        if(!(Test-Path -LiteralPath $retentionTarget)){continue}
        $entries=@(Get-Item -LiteralPath $retentionTarget -Force);if($entries[0].PSIsContainer){$entries+=@(Get-ChildItem -LiteralPath $retentionTarget -Force -Recurse)}
        if(@($entries | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Linked diagnostics require separate review and are retained'}
        if($Apply){if($entries[0].PSIsContainer){Remove-Item -LiteralPath $retentionTarget -Recurse -Force}else{Remove-Item -LiteralPath $retentionTarget -Force};$retentionReport.Removed+=@($retentionTarget)}
    }
    $retentionReport.Retained+=@($package.retained_linked_diagnostics)
}
foreach($drive in @('C','D','E')){$retentionReport.AfterFree[$drive]=(Get-PSDrive $drive).Free}
$retentionReport | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $retentionRepo 'artifacts/player-retention-20261031/result.json') -Encoding UTF8
Write-Output ('Audited package children removed: '+$retentionReport.Removed.Count)
