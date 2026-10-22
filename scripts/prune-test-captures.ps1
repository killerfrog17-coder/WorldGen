param([switch]$Apply)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
if((Get-Item -LiteralPath $artifactRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Artifacts must be a real directory.'}
if([IO.Path]::GetPathRoot($artifactRoot) -ne 'C:\'){throw 'Scope is C: artifacts only.'}
if(@(git -C $repoRoot ls-files -- artifacts).Count){throw 'Tracked artifact data requires separate review.'}
# Retain all player files, the packaged worker, research tools/references, current
# split-grass evidence and the current build log. No links are traversed.
$pending=[Collections.Generic.Stack[string]]::new();$pending.Push($artifactRoot)
$targets=[Collections.Generic.List[object]]::new()
while($pending.Count){foreach($entry in Get-ChildItem -LiteralPath $pending.Pop() -Force){
    if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint){continue}
    $full=[IO.Path]::GetFullPath($entry.FullName)
    if(!$full.StartsWith($artifactRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Outside artifact boundary: $full"}
    if($entry.PSIsContainer){
        if($entry.Parent.FullName -eq $artifactRoot -and ($entry.Name -like 'player-*' -or $entry.Name -in @('terrain-worker','research-pdf-tools','short-grass-native'))){continue}
        $pending.Push($full);continue
    }
    if($entry.Name -eq 'unity-build.log'){continue}
    if($entry.Extension.ToLowerInvariant() -notin @('.png','.jpg','.jpeg','.bmp','.log','.etl')){continue}
    $targets.Add([pscustomobject]@{Path=$full;Bytes=$entry.Length})
}}
$bytes=0L;foreach($target in $targets){$bytes+=$target.Bytes}
Write-Output ("Old test captures/logs: {0} files / {1:N2} GiB. Player files, worlds, audio, CSV/JSON/TXT results and research references retained." -f $targets.Count,($bytes/1GB))
if(!$Apply){Write-Output 'DRY RUN; no deletion.';return}
# This is a one-shot owner-authorized cleanup, not automatic startup behaviour.
foreach($target in $targets){
    $item=Get-Item -LiteralPath $target.Path -Force
    if($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw "Target changed: $($target.Path)"}
    Remove-Item -LiteralPath $target.Path -Force
}
$report=[ordered]@{Utc=[DateTime]::UtcNow.ToString('o');Scope=$artifactRoot;DeletedCount=$targets.Count;DeletedBytes=$bytes;Files=$targets.ToArray();Recoverable=$false}
$report|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $artifactRoot 'test-capture-cleanup.json') -Encoding UTF8
Write-Output 'Deleted only listed obsolete test captures/logs; files can be regenerated but are not in Recycle Bin.'
