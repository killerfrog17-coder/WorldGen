param([Parameter(Mandatory=$true)][string]$Plan,[switch]$Apply)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$allowed=[IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')).TrimEnd('\')
$planPath=[IO.Path]::GetFullPath($Plan)
if(!$planPath.StartsWith($allowed+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Plan outside artifacts'}
$data=Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
if([IO.Path]::GetFullPath($data.allowed_root).TrimEnd('\')-ne$allowed){throw 'Unexpected cleanup root'}
$protected=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($name in $data.protected){
 $p=[IO.Path]::GetFullPath($name).TrimEnd('\')
 while($p-and$p.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){
  $null=$protected.Add($p);$p=[IO.Path]::GetDirectoryName($p)
 }
}
$running=@(Get-CimInstance Win32_Process | Where-Object {$_.ExecutablePath})
$checked=[Collections.Generic.List[object]]::new()
foreach($target in $data.remove){
 $full=[IO.Path]::GetFullPath($target.path).TrimEnd('\')
 if(!$full.StartsWith($allowed+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Target escaped artifacts: $full"}
 if($protected.Contains($full)){throw "Target overlaps protected assets: $full"}
 if(!(Test-Path -LiteralPath $full)){continue}
 $resolved=(Resolve-Path -LiteralPath $full).ProviderPath.TrimEnd('\')
 if($resolved-ne$full){throw "Resolved target differs: $full"}
 foreach($process in $running){if($process.ExecutablePath-eq$full-or$process.ExecutablePath.StartsWith($full+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Running executable inside target: $full"}}
 $items=@(Get-Item -LiteralPath $full -Force)
 if($items[0].PSIsContainer){$items+=@(Get-ChildItem -LiteralPath $full -Recurse -Force)}
 foreach($item in $items){
  if($item.Attributes-band[IO.FileAttributes]::ReparsePoint){throw "Refusing linked target: $($item.FullName)"}
  if($item.Name-eq'.git'){throw "Source checkout protected: $full"}
 }
 $bytes=0L;foreach($item in $items){if(!$item.PSIsContainer){$bytes+=$item.Length}}
 $checked.Add([pscustomobject]@{path=$full;bytes=$bytes;reason=$target.reason;directory=$items[0].PSIsContainer})
}
if(!$Apply){Write-Output ('Validated {0} targets, {1:N3} GiB; no files changed.' -f $checked.Count,(($checked|Measure-Object bytes -Sum).Sum/1GB));return}
$reportPath=Join-Path $allowed 'fauna-cleanup-report-20261026.json'
$removed=[Collections.Generic.List[object]]::new();$failed=[Collections.Generic.List[object]]::new()
$report=[ordered]@{scope=$data.scope;applied=$true;started=(Get-Date).ToString('o');removed=@();failed=@();removed_bytes=0L;retained_builds=$data.retained_builds}
function Save-Report {
 $report.removed=@($removed.ToArray());$report.failed=@($failed.ToArray());$report.updated=(Get-Date).ToString('o')
 $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $reportPath -Encoding UTF8
}
Save-Report
foreach($target in $checked){
 try{
  if($target.directory){Remove-Item -LiteralPath $target.path -Recurse -Force -Confirm:$false}else{Remove-Item -LiteralPath $target.path -Force -Confirm:$false}
  if(Test-Path -LiteralPath $target.path){throw 'Target remains after removal'}
  $removed.Add($target);$report.removed_bytes+=$target.bytes
 }catch{$failed.Add([pscustomobject]@{path=$target.path;error=$_.Exception.Message})}
 if((($removed.Count+$failed.Count)%50)-eq0){Save-Report;Write-Output ('Removed {0}/{1}; {2:N2} GiB.' -f $removed.Count,$checked.Count,($report.removed_bytes/1GB))}
}
Save-Report
Write-Output ('Cleanup complete: {0} removed, {1} failed, {2:N3} GiB.' -f $removed.Count,$failed.Count,($report.removed_bytes/1GB))
if($failed.Count){exit 1}
