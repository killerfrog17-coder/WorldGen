param([switch]$Apply)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot=Join-Path $repo 'artifacts'
$roots=@($artifactRoot,'E:\WorldGenBuilds') | Where-Object {Test-Path -LiteralPath $_}
# An unfinished player can already have its executable/data directory but no
# final timestamp manifest. Never classify or remove it during a Unity build.
$project=([IO.Path]::GetFullPath((Join-Path $repo 'unity/WorldGen.Unity'))).Replace('\','/')
if(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -and $_.CommandLine.Replace('\','/').IndexOf($project,[StringComparison]::OrdinalIgnoreCase) -ge 0}){throw 'Wait for this project editor/build to exit before cleaning player artifacts.'}
$protected=@()
$shortcut=Join-Path $repo 'WorldGen.exe - Shortcut.lnk'
if(Test-Path -LiteralPath $shortcut){$target=(New-Object -ComObject WScript.Shell).CreateShortcut($shortcut).TargetPath;if($target){$protected+=Split-Path -Parent $target}}
foreach($proc in Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe' OR Name='WorldGen.Cli.exe'"){
 if(!$proc.ExecutablePath){throw 'Cannot resolve a running game/worker executable; leave all builds in place.'}
 $protected+=Split-Path -Parent $proc.ExecutablePath
}
$builds=@(foreach($root in $roots){foreach($folder in Get-ChildItem -LiteralPath $root -Directory){
 $exe=Join-Path $folder.FullName 'WorldGen.exe';$data=Join-Path $folder.FullName 'WorldGen_Data'
 if(!(Test-Path -LiteralPath $exe) -or !(Test-Path -LiteralPath $data)){continue}
 $stamp=(Get-Item -LiteralPath $exe).LastWriteTime
 $manifest=Join-Path $folder.FullName 'build-inputs.json'
 if(Test-Path -LiteralPath $manifest){$built=(Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json).BuiltUtc;if($built){$stamp=([DateTime]::Parse($built)).ToLocalTime()}}
 [pscustomobject]@{Path=$folder.FullName;Stamp=$stamp;Day=$stamp.ToString('yyyy-MM-dd')}
}})
$retained=@($builds|Group-Object Day|ForEach-Object {$_.Group|Sort-Object Stamp -Descending|Select-Object -First 1})+@($builds|Sort-Object Stamp -Descending|Select-Object -First 3)
$protected+=@($retained|ForEach-Object {$_.Path})
$protected=@($protected|Sort-Object -Unique)
$report=[ordered]@{Created=[DateTime]::UtcNow.ToString('o');Applied=[bool]$Apply;Rule='Latest build per local day plus latest three; selected and running builds protected; source assets/saves untouched';Retained=$protected;Removed=@();Bytes=0L}
$reportPath=Join-Path $artifactRoot ('daily-cleanup-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')+'.json')
try {
foreach($build in $builds){
 $full=[IO.Path]::GetFullPath($build.Path).TrimEnd('\')
 if($full -in $protected -or @($protected|Where-Object {$_.StartsWith($full+'\',[StringComparison]::OrdinalIgnoreCase)}).Count){continue}
 if(!@($roots|Where-Object {$full.StartsWith(([IO.Path]::GetFullPath($_).TrimEnd('\')+'\'),[StringComparison]::OrdinalIgnoreCase)}).Count){throw "Outside cleanup roots: $full"}
 if((Resolve-Path -LiteralPath $full).ProviderPath -ne $full){throw "Resolution mismatch: $full"}
 $entries=@(Get-Item -LiteralPath $full)+@(Get-ChildItem -LiteralPath $full -Recurse -Force)
 if(@($entries|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw "Linked target: $full"}
 $bytes=($entries|Where-Object {!$_.PSIsContainer}|Measure-Object Length -Sum).Sum
 if($Apply){
  # Recheck immediately before mutation; an owner may have launched a retained
  # older player or its nested terrain worker after the initial inventory.
  $live=@(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe' OR Name='WorldGen.Cli.exe'")
  if(@($live|Where-Object {!$_.ExecutablePath}).Count){throw 'Cannot resolve a newly running executable; stop cleanup.'}
  if(@($live|Where-Object {$_.ExecutablePath.StartsWith($full+'\',[StringComparison]::OrdinalIgnoreCase)}).Count){$protected+=$full;continue}
  Remove-Item -LiteralPath $full -Recurse -Force
 }
 $report.Removed+=@{Path=$full;Bytes=$bytes};$report.Bytes+=$bytes
 Write-Output ("{0} {1:N2} GiB {2}" -f $(if($Apply){'Removed'}else{'Would remove'}),($bytes/1GB),$full)
}
} catch {$report['Failure']=$_.Exception.Message;throw}
finally {
 $report.Retained=$protected
 $report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $reportPath -Encoding UTF8
}
Write-Output ("{0} builds; {1:N2} GiB; {2} protected daily/selected/running paths" -f $report.Removed.Count,($report.Bytes/1GB),$protected.Count)
Write-Output ('Manifest: '+$reportPath)
