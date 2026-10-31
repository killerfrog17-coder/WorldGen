param([switch]$Apply)
$ErrorActionPreference='Stop'
$trashRepo=Split-Path -Parent $PSScriptRoot
$trashPlan=Get-Content -Raw -LiteralPath (Join-Path $trashRepo 'artifacts/player-retention-20261031/test-output-plan.json') | ConvertFrom-Json
$trashProtected=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($p in $trashPlan.protected){[void]$trashProtected.Add([IO.Path]::GetFullPath($p))}
$trashRemoved=0;$trashBytes=0L
foreach($row in $trashPlan.files){
    $trashPath=[IO.Path]::GetFullPath($row.path)
    if(!@($trashPlan.roots | Where-Object {$trashPath.StartsWith([IO.Path]::GetFullPath($_).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)}).Count){throw 'Output outside audited roots'}
    if($trashProtected.Contains($trashPath)){throw 'Required source/input protected'}
    if(!(Test-Path -LiteralPath $trashPath -PathType Leaf)){continue}
    $trashEntry=Get-Item -LiteralPath $trashPath -Force
    if($trashEntry.Attributes -band [IO.FileAttributes]::ReparsePoint){continue}
    if($Apply){Remove-Item -LiteralPath $trashPath -Force;$trashRemoved++;$trashBytes+=$row.bytes}
}
[ordered]@{Applied=[bool]$Apply;RemovedFiles=$trashRemoved;RemovedLogicalBytes=$trashBytes}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $trashRepo 'artifacts/player-retention-20261031/test-output-result.json') -Encoding UTF8
Write-Output ('Removed '+$trashRemoved+' regenerable files; '+[Math]::Round($trashBytes/1GB,2)+' GiB logical')
