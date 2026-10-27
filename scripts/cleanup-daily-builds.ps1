param([switch]$Apply)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot=Join-Path $repo 'artifacts'
$roots=@($artifactRoot,'E:\WorldGenBuilds') | Where-Object {Test-Path -LiteralPath $_}
$protected=@()
$shortcut=Join-Path $repo 'WorldGen.exe - Shortcut.lnk'
if(Test-Path -LiteralPath $shortcut){$target=(New-Object -ComObject WScript.Shell).CreateShortcut($shortcut).TargetPath;if($target){$protected+=Split-Path -Parent $target}}
foreach($proc in Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe' OR Name='WorldGen.Cli.exe'"){if($proc.ExecutablePath){$protected+=Split-Path -Parent $proc.ExecutablePath}}
$builds=@(foreach($root in $roots){foreach($folder in Get-ChildItem -LiteralPath $root -Directory){
 $exe=Join-Path $folder.FullName 'WorldGen.exe';$data=Join-Path $folder.FullName 'WorldGen_Data'
 if(!(Test-Path -LiteralPath $exe) -or !(Test-Path -LiteralPath $data)){continue}
 $stamp=(Get-Item -LiteralPath $exe).LastWriteTime
 $manifest=Join-Path $folder.FullName 'build-inputs.json'
 if(Test-Path -LiteralPath $manifest){$built=(Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json).BuiltUtc;if($built){$stamp=([DateTime]::Parse($built)).ToLocalTime()}}
 [pscustomobject]@{Path=$folder.FullName;Stamp=$stamp;Day=$stamp.ToString('yyyy-MM-dd')}
}})
$retained=@($builds|Group-Object Day|ForEach-Object {$_.Group|Sort-Object Stamp -Descending|Select-Object -First 1})
$protected+=@($retained|ForEach-Object {$_.Path})
$report=[ordered]@{Created=[DateTime]::UtcNow.ToString('o');Applied=[bool]$Apply;Rule='Latest build per local day, selected and running builds protected; source assets/saves untouched';Retained=$protected;Removed=@();Bytes=0L}
foreach($build in $builds){
 $full=[IO.Path]::GetFullPath($build.Path).TrimEnd('\')
 if($full -in $protected){continue}
 if(!@($roots|Where-Object {$full.StartsWith(([IO.Path]::GetFullPath($_).TrimEnd('\')+'\'),[StringComparison]::OrdinalIgnoreCase)}).Count){throw "Outside cleanup roots: $full"}
 if((Resolve-Path -LiteralPath $full).ProviderPath -ne $full){throw "Resolution mismatch: $full"}
 $entries=@(Get-Item -LiteralPath $full)+@(Get-ChildItem -LiteralPath $full -Recurse -Force)
 if(@($entries|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw "Linked target: $full"}
 $bytes=($entries|Where-Object {!$_.PSIsContainer}|Measure-Object Length -Sum).Sum
 if($Apply){Remove-Item -LiteralPath $full -Recurse -Force}
 $report.Removed+=@{Path=$full;Bytes=$bytes};$report.Bytes+=$bytes
 Write-Output ("{0} {1:N2} GiB {2}" -f $(if($Apply){'Removed'}else{'Would remove'}),($bytes/1GB),$full)
}
$report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $artifactRoot 'daily-cleanup-20261027.json') -Encoding UTF8
Write-Output ("{0} builds; {1:N2} GiB; {2} protected daily/selected/running paths" -f $report.Removed.Count,($report.Bytes/1GB),$protected.Count)
