param([switch]$Apply,[switch]$CopyRetainedToC,[switch]$KeepLatestThree)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
if((Get-Item -LiteralPath $artifactRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Artifacts must be a real directory, not a link.'}
if([IO.Path]::GetPathRoot($artifactRoot) -ne 'C:\'){throw 'This cleanup is scoped to the C: artifacts folder.'}
$beforeC=(Get-PSDrive C).Free
function Assert-ArtifactChild([string]$path){
    $full=[IO.Path]::GetFullPath($path)
    if([IO.Path]::GetDirectoryName($full) -ne $artifactRoot -or !$full.StartsWith($artifactRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Outside direct artifact children: $full"}
    return $full
}
function Read-Tree([string]$path){
    # This function is read-only even for an external source. Never follow nested links.
    $pending=[Collections.Generic.Stack[string]]::new();$pending.Push($path)
    $files=[Collections.Generic.List[object]]::new();$bytes=0L
    while($pending.Count){foreach($entry in Get-ChildItem -LiteralPath $pending.Pop() -Force){
        if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Nested link requires separate review: $($entry.FullName)"}
        if($entry.Name -eq '.git'){throw "Source checkout is not a disposable player build: $path"}
        if($entry.PSIsContainer){$pending.Push($entry.FullName)}else{$bytes+=$entry.Length;$files.Add($entry)}
    }}
    return [pscustomobject]@{Bytes=$bytes;Files=$files.ToArray()}
}
function Assert-Idle($build){
    foreach($p in Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe' OR Name='WorldGen.Cli.exe'"){
        if(!$p.ExecutablePath){continue}
        foreach($prefix in @($build.Path,$build.Source)){
            if($p.ExecutablePath.StartsWith($prefix+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Build is running (PID $($p.ProcessId)); no mutation allowed: $($build.Name)"}
        }
    }
}
function Remove-ArtifactBuild($build){
    $full=Assert-ArtifactChild $build.Path;Assert-Idle $build
    $item=Get-Item -LiteralPath $full -Force
    if($build.Linked){
        if(!($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -or [IO.Path]::GetFullPath([string]$item.Target[0]) -ne $build.Source){throw "Link target changed: $full"}
        # NONRECURSIVE link removal only. The external target is never deleted.
        [IO.Directory]::Delete($full)
    }else{
        if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Build became a link: $full"}
        $null=Read-Tree $full
        Remove-Item -LiteralPath $full -Recurse -Force
    }
}
$tracked=@(git -C $repoRoot ls-files -- artifacts)
if($LASTEXITCODE -ne 0 -or $tracked.Count){throw 'Cannot prove artifact build outputs are untracked.'}
$builds=@(foreach($dir in Get-ChildItem -LiteralPath $artifactRoot -Directory -Force){
    $path=Assert-ArtifactChild $dir.FullName
    if(!(Test-Path -LiteralPath (Join-Path $path 'WorldGen.exe') -PathType Leaf) -or !(Test-Path -LiteralPath (Join-Path $path 'WorldGen_Data') -PathType Container)){continue}
    $linked=[bool]($dir.Attributes -band [IO.FileAttributes]::ReparsePoint)
    $source=if($linked){[IO.Path]::GetFullPath([string]$dir.Target[0])}else{$path}
    $timestamp=(Get-Item -LiteralPath (Join-Path $source 'WorldGen.exe')).LastWriteTime
    $manifest=Join-Path $source 'build-inputs.json'
    if(Test-Path -LiteralPath $manifest -PathType Leaf){
        $meta=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
        if($meta.BuiltUtc){$timestamp=[DateTimeOffset]::Parse($meta.BuiltUtc).LocalDateTime}
    }
    $tree=Read-Tree $source
    [pscustomobject]@{Name=$dir.Name;Path=$path;Source=$source;Linked=$linked;Timestamp=$timestamp;Day=$timestamp.ToString('yyyy-MM-dd');Bytes=$tree.Bytes}
})
if(!$builds.Count){throw 'No playable checkpoints found; refusing an empty retention plan.'}
$keep=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($day in $builds | Group-Object Day){$null=$keep.Add(($day.Group | Sort-Object Timestamp,Name -Descending | Select-Object -First 1).Name)}
if($KeepLatestThree){foreach($build in $builds | Sort-Object Timestamp,Name -Descending | Select-Object -First 3){$null=$keep.Add($build.Name)}}
# A launcher may intentionally select a rollback. Protect it in addition to the retention rule.
$shortcut=Join-Path $repoRoot 'WorldGen.exe - Shortcut.lnk'
if(Test-Path -LiteralPath $shortcut){
    $shell=New-Object -ComObject WScript.Shell;$selected=$shell.CreateShortcut($shortcut).TargetPath
    foreach($build in $builds){if($selected.StartsWith($build.Path+'\',[StringComparison]::OrdinalIgnoreCase)){$null=$keep.Add($build.Name)}}
}
$running=@(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe' OR Name='WorldGen.Cli.exe'")
foreach($build in $builds){foreach($p in $running){foreach($prefix in @($build.Path,$build.Source)){
    if($p.ExecutablePath -and $p.ExecutablePath.StartsWith($prefix+'\',[StringComparison]::OrdinalIgnoreCase)){$null=$keep.Add($build.Name)}
}}}
$retained=@($builds | Where-Object {$keep.Contains($_.Name)})
$remove=@($builds | Where-Object {!$keep.Contains($_.Name)})
$localRemoved=0L;foreach($build in $remove){if(!$build.Linked){$localRemoved+=$build.Bytes}}
$copyBytes=0L;if($CopyRetainedToC){foreach($build in $retained){if($build.Linked){$copyBytes+=$build.Bytes}}}
$report=[ordered]@{
    Applied=[bool]$Apply;Scope=$artifactRoot;LocalDayTimeZone=[TimeZoneInfo]::Local.Id
    Rule=$(if($KeepLatestThree){'Newest playable checkpoint per local calendar date plus newest three overall; selected/running builds protected.'}else{'Newest playable checkpoint per local calendar date only; selected/running builds protected.'})
    Found=$builds.Count;Retained=$retained;Remove=$remove;LocalRemovedBytes=$localRemoved;CopiedBytes=$copyBytes
    ExternalOriginalsDeleted=$false;OtherArtifactEntriesDeleted=$false;BeforeCFreeBytes=$beforeC
    RemovedNames=[Collections.Generic.List[string]]::new();MigratedNames=[Collections.Generic.List[string]]::new();PendingMigration=[Collections.Generic.List[string]]::new()
}
$reportPath=Join-Path $artifactRoot 'checkpoint-retention.json'
function Save-Report{$report.AfterCFreeBytes=(Get-PSDrive C).Free;$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding UTF8}
$retained | Sort-Object Timestamp | Format-Table Name,Timestamp,Linked -AutoSize
Write-Output ("Found {0}; keep {1}; remove {2} ({3:N2} GiB physically on C:); copy {4:N2} GiB back to C:." -f $builds.Count,$retained.Count,$remove.Count,($localRemoved/1GB),($copyBytes/1GB))
if(!$Apply){Write-Output 'DRY RUN: no deletion, copy or report write.';return}
if($beforeC+$localRemoved-$copyBytes -lt 1GB){throw 'Not enough safe C: capacity for retention/copy plan.'}
Save-Report
try{
    # Real obsolete C: outputs first, to make space. Do not touch other artifact data.
    foreach($build in $remove | Sort-Object Linked,Timestamp){
        Remove-ArtifactBuild $build;$report.RemovedNames.Add($build.Name);Save-Report
        Write-Output ("Removed {0}{1}" -f $build.Name,$(if($build.Linked){' (link only; external original untouched)'}else{''}))
    }
    if($CopyRetainedToC){foreach($build in $retained | Where-Object Linked){
        try{Assert-Idle $build}catch{$report.PendingMigration.Add($build.Name);Write-Output $_;continue}
        if((Get-PSDrive C).Free -lt $build.Bytes+1GB){throw "Insufficient space before copying $($build.Name)"}
        $staging=Assert-ArtifactChild (Join-Path $artifactRoot ('checkpoint-copy-'+[Guid]::NewGuid().ToString('N')))
        New-Item -ItemType Directory -Path $staging | Out-Null
        $tree=Read-Tree $build.Source
        foreach($file in $tree.Files){
            $relative=$file.FullName.Substring($build.Source.TrimEnd('\').Length+1)
            $destination=[IO.Path]::GetFullPath((Join-Path $staging $relative))
            if(!$destination.StartsWith($staging+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Copy path escaped artifact staging.'}
            $parent=[IO.Path]::GetDirectoryName($destination);if(!(Test-Path -LiteralPath $parent)){New-Item -ItemType Directory -Path $parent -Force | Out-Null}
            Copy-Item -LiteralPath $file.FullName -Destination $destination
            if((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash){throw "Copied file differs: $relative"}
        }
        $copied=Read-Tree $staging
        if($copied.Files.Count -ne $tree.Files.Count -or $copied.Bytes -ne $tree.Bytes){throw 'Copied build inventory differs.'}
        Assert-Idle $build
        Remove-ArtifactBuild $build # removes only the junction, never its source
        Move-Item -LiteralPath $staging -Destination (Assert-ArtifactChild $build.Path)
        if((Get-Item -LiteralPath $build.Path).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Retained build is still linked.'}
        $report.MigratedNames.Add($build.Name);Save-Report
        Write-Output ("Restored real C: folder, all file hashes verified: {0}; external original untouched." -f $build.Name)
    }}
}finally{Save-Report}
Write-Output ("Completed: {0} obsolete checkpoint entries removed; {1} retained builds copied to C:; C: free {2:N2} GiB. Report: {3}" -f $report.RemovedNames.Count,$report.MigratedNames.Count,((Get-PSDrive C).Free/1GB),$reportPath)
