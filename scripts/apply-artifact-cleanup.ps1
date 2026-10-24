param([Parameter(Mandatory=$true)][string]$Plan,[switch]$Apply)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$planPath=[IO.Path]::GetFullPath($Plan)
if(!$planPath.StartsWith((Join-Path $repoRoot 'artifacts')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Plan must be inside workspace artifacts.'}
$data=Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
$expectedRoots=@((Join-Path $repoRoot 'artifacts'),'E:\WorldGenBuilds','D:\WorldGenAI')
$roots=@($data.allowed_roots | ForEach-Object {[IO.Path]::GetFullPath($_).TrimEnd('\')})
foreach($root in $roots){if($root -notin $expectedRoots){throw "Unexpected cleanup root: $root"}}
$protected=@($data.protected | ForEach-Object {[IO.Path]::GetFullPath($_).TrimEnd('\')})
$reportPath=Join-Path $repoRoot 'artifacts/cleanup-report-20261024.json'
$report=[ordered]@{Applied=[bool]$Apply;Rule=$data.rule;BeforeFree=@{};AfterFree=@{};Removed=@();Skipped=@();RemovedBytes=0L;RetainedBuilds=$data.retained_builds}
foreach($drive in @('C','D','E')){$report.BeforeFree[$drive]=(Get-PSDrive $drive).Free}
function Save-Report {
    foreach($drive in @('C','D','E')){$report.AfterFree[$drive]=(Get-PSDrive $drive).Free}
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8
}
function Verify-Target([string]$path){
    $full=[IO.Path]::GetFullPath($path).TrimEnd('\')
    if(!@($roots | Where-Object {$full.StartsWith($_+'\',[StringComparison]::OrdinalIgnoreCase)}).Count){throw "Target outside named cleanup roots: $full"}
    foreach($keep in $protected){
        if($full -eq $keep -or $keep.StartsWith($full+'\',[StringComparison]::OrdinalIgnoreCase) -or $full.StartsWith($keep+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Target overlaps protected data: $full"}
    }
    $resolved=(Resolve-Path -LiteralPath $full).ProviderPath
    if($resolved -ne $full){throw "Resolved path mismatch: $full -> $resolved"}
    $entries=@(Get-Item -LiteralPath $full -Force)
    if($entries[0].PSIsContainer){$entries+=@(Get-ChildItem -LiteralPath $full -Recurse -Force)}
    foreach($entry in $entries){if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Linked target requires separate review: $($entry.FullName)"}}
    foreach($process in Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe' OR Name='WorldGen.Cli.exe'"){
        if($process.ExecutablePath -and $process.ExecutablePath.StartsWith($full+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Running game/CLI protected: $full"}
    }
    # The selected launcher can change while another source agent packages.
    $shortcut=Join-Path $repoRoot 'WorldGen.exe - Shortcut.lnk'
    if(Test-Path -LiteralPath $shortcut){
        $selected=(New-Object -ComObject WScript.Shell).CreateShortcut($shortcut).TargetPath
        if($selected.StartsWith($full+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Currently selected game protected: $full"}
    }
    $bytes=0L;foreach($entry in $entries){if(!$entry.PSIsContainer){$bytes+=$entry.Length}}
    return [pscustomobject]@{Path=$full;Bytes=$bytes;Directory=$entries[0].PSIsContainer}
}
if(!$Apply){Write-Output ("DRY RUN: {0} explicit targets; no files changed." -f $data.remove.Count);return}
Save-Report
foreach($target in $data.remove){
    if(!(Test-Path -LiteralPath $target.path)){continue}
    try{
        $checked=Verify-Target $target.path
        if($checked.Directory){Remove-Item -LiteralPath $checked.Path -Recurse -Force}else{Remove-Item -LiteralPath $checked.Path -Force}
        $report.Removed+=@([ordered]@{Path=$checked.Path;Bytes=$checked.Bytes;Reason=$target.reason});$report.RemovedBytes+=$checked.Bytes
        Write-Output ('Removed {0:N3} GiB {1}' -f ($checked.Bytes/1GB),$checked.Path)
    }catch{
        $report.Skipped+=@([ordered]@{Path=$target.path;Reason=$_.Exception.Message})
        Write-Output ('SKIPPED '+$target.path+' :: '+$_.Exception.Message)
    }
    Save-Report
}
Save-Report
Write-Output ('Cleanup complete: {0} targets removed, {1:N2} GiB; {2} skipped.' -f $report.Removed.Count,($report.RemovedBytes/1GB),$report.Skipped.Count)
