param([switch]$Apply)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$localRoot=Join-Path $repoRoot 'artifacts'
$externalRoot='D:\WorldGenBuildArtifacts'
$specialTargets=@('E:\WorldGen-history18-review','D:\WorldGen-local-history-20261013\rollback-history9')
$report=[Collections.Generic.List[object]]::new()
$scheduled=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$before=@{};foreach($drive in @('C','D','E')){$d=Get-PSDrive $drive -ErrorAction SilentlyContinue;if($d){$before[$drive]=$d.Free}}
function Safe-Child([string]$root,[string]$relative){
    $parent=[IO.Path]::GetFullPath($root).TrimEnd('\');$full=[IO.Path]::GetFullPath((Join-Path $parent $relative))
    if(!$full.StartsWith($parent+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Path escaped review root: $full"};return $full
}
function Assert-Disposable([string]$full){
    $allowed=$false
    foreach($root in @($localRoot,$externalRoot)){$p=[IO.Path]::GetFullPath($root).TrimEnd('\');if($full.StartsWith($p+'\',[StringComparison]::OrdinalIgnoreCase)){$allowed=$true}}
    foreach($exact in $specialTargets){if($full.Equals($exact,[StringComparison]::OrdinalIgnoreCase)){$allowed=$true}}
    if(!$allowed){throw "Deletion outside explicitly scoped test artifacts: $full"}
}
function Remove-ReviewItem([string]$path){
    $full=[IO.Path]::GetFullPath($path);Assert-Disposable $full
    if($scheduled.Contains($full)){return}
    if(!(Test-Path -LiteralPath $full)){return};$item=Get-Item -LiteralPath $full -Force
    if(($item.Attributes-band[IO.FileAttributes]::ReparsePoint)-ne0){throw "Refusing recursive removal through a junction: $full"}
    # Inspect one directory level at a time: never follow an unexpected link or
    # remove an embedded source checkout while cleaning generated outputs.
    $files=[Collections.Generic.List[object]]::new()
    if($item.PSIsContainer){
        $pending=[Collections.Generic.Stack[string]]::new();$pending.Push($full)
        while($pending.Count){foreach($child in Get-ChildItem -LiteralPath $pending.Pop() -Force){
            if($child.Name-eq'.git'){throw "Source checkout is protected: $full"}
            if(($child.Attributes-band[IO.FileAttributes]::ReparsePoint)-ne0){throw "Unexpected nested link: $($child.FullName)"}
            if($child.PSIsContainer){$pending.Push($child.FullName)}else{$files.Add($child)}
        }}
    }else{$files.Add($item)}
    $bytes=($files|Measure-Object Length -Sum).Sum
    $report.Add([pscustomobject][ordered]@{Path=$full;Bytes=[long]$bytes;Files=$files.Count})
    $null=$scheduled.Add($full)
    if($Apply){Remove-Item -LiteralPath $full -Recurse -Force -Confirm:$false}
}
function Prune-Directories([string]$root,[string[]]$keep){
    if(!(Test-Path -LiteralPath $root)){return}
    foreach($dir in Get-ChildItem -LiteralPath $root -Directory -Force){
        if($root-eq$localRoot-and$knownLinks.ContainsKey($dir.Name)){continue}
        if($keep-notcontains$dir.Name){Remove-ReviewItem $dir.FullName}
    }
}
$keepImages=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($relative in @(
 'history32-v-earth200-replay/year-129600/states.png','history32-v-earth200-replay/year-129600/technology.png',
 'history32-v-earth200-replay/year-121900/states.png','history32-v-cp200-replay/year-171700/states.png',
 'history32-v-cp200-replay/year-171700/technology.png','history32-v-cp200-replay/year-170200/states.png',
 'artifacts/history32/round-v-pangeae31415/year-134000/states.png','artifacts/history32/round-v-iceage27182/year-445000/states.png',
 'artifacts/history32/living-v-browser/harbor-supply.png','artifacts/history32/living-v-browser/harbor-footprint.png',
 'artifacts/history32/living-v-browser/harbor-food-flows.png','artifacts/history32/living-v-browser/compact-reader.png',
 'artifacts/history32/unity-u2-cp-performance1500/playback-map.png','artifacts/history32/unity-v-cp-performance1500/playback-map.png'
)){$null=$keepImages.Add((Safe-Child $externalRoot $relative))}
foreach($relative in @(
 'history32/fresh-w2-earth200/year-121900/states.png','history32/fresh-w2-earth200/year-128200/states.png',
 'history32/fresh-w2-earth200/year-129600/states.png','history32/fresh-w2-earth200/year-129600/technology.png',
 'history32/fresh-w2-cp200/year-172500/states.png','history32/fresh-w2-cp200/year-177500/states.png',
 'history32/fresh-w2-cp200/year-179000/states.png','history32/fresh-w2-cp200/year-179000/technology.png',
 'history32/stopping-w-earth500/baseline/states.png','history32/stopping-w-earth500/candidate/states.png',
 'history32/stopping-w-cp500/baseline/states.png','history32/stopping-w-cp500/candidate/states.png',
 'history32/living-w2-browser/harbor-supply.png','history32/living-w2-browser/harbor-footprint.png',
 'history32/living-w2-browser/harbor-food-flows.png','history32/living-w2-browser/compact-reader.png'
)){$null=$keepImages.Add((Safe-Child $localRoot $relative))}
$null=$keepImages.Add((Safe-Child $externalRoot 'artifacts/history32/unity-w2-fresh-cp-performance1000/playback-map.png'))
$null=$keepImages.Add((Safe-Child $externalRoot 'artifacts/history32/unity-w2-complete-cp-performance1000/playback-map.png'))
function Prune-Evidence([string]$root,[string[]]$worldNames=@('final.world')){
    if(!(Test-Path -LiteralPath $root)){return}
    foreach($file in Get-ChildItem -LiteralPath $root -Recurse -File -Force){
        $image=$file.Extension.ToLowerInvariant()-in@('.png','.jpg','.jpeg','.webp','.bmp')
        $world=$file.Extension-eq'.world'
        $delete=($image-and!$keepImages.Contains($file.FullName))-or$file.Extension-eq'.svg'-or$file.Name.EndsWith('.svg.gz')
        if($world-and!($file.DirectoryName-eq[IO.Path]::GetFullPath($root)-and$worldNames-contains$file.Name)){$delete=$true}
        if($delete){Remove-ReviewItem $file.FullName}
    }
}
$active=Get-CimInstance Win32_Process | Where-Object{($_.Name-eq'WorldGen.exe')-or($_.Name-eq'Unity.exe'-and$_.CommandLine-and!$_.CommandLine.Contains('-adb2')-and$_.CommandLine.Contains('WorldGen.Unity'))}
if($active){throw 'Close the active player/project editor before deleting test artifacts.'}
# This is the explicitly reviewed history32 retention manifest, not an
# unattended janitor. Do not delete a later release using today's keep list.
foreach($root in @($localRoot,$externalRoot)){
    if(Test-Path -LiteralPath $root){foreach($dir in Get-ChildItem -LiteralPath $root -Directory){
        if($dir.Name-match'^(?:player-)?history-?(\d+)' -and [int]$Matches[1]-gt32){throw 'A newer history version exists. Review the retention manifest before applying this cleanup.'}
    }}
}
# Known old junctions require exact target verification, separate deletion of
# the actual disposable target, then nonrecursive removal of the link itself.
$knownLinks=@{'history18-review'='E:\WorldGen-history18-review';'player-history-9'='D:\WorldGen-local-history-20261013\rollback-history9';'player-history-32-v-il2cpp'='D:\WorldGenBuildArtifacts\artifacts\player-history-32-v-il2cpp'}
foreach($name in $knownLinks.Keys){$link=Safe-Child $localRoot $name;if(Test-Path -LiteralPath $link){$item=Get-Item -LiteralPath $link -Force
    if(($item.Attributes-band[IO.FileAttributes]::ReparsePoint)-eq0){throw "Expected a known junction: $link"}
    if([IO.Path]::GetFullPath([string]$item.Target[0])-ne$knownLinks[$name]){throw "Junction target changed: $link"}
    Remove-ReviewItem $knownLinks[$name];if($Apply){[IO.Directory]::Delete($link)}
}}
Prune-Directories $localRoot @('history31','history32','player-history-31-environment','player-history-32-review-v','player-history-32-review-u2','player-history-32-review-w2','climate-learning','latest')
$localHistory=Safe-Child $localRoot 'history32'
Prune-Directories $localHistory @('checkpoint-v-bin','native-h-current-controls','round-n-archipelago42','round-n-aborea73','round-n-lakes17','fresh-w2-earth200','fresh-w2-cp200','fresh-w2-cp200-maxtech','stopping-w-earth500','stopping-w-cp500','living-w2-browser','unity-w2-dock')
foreach($dir in @('round-n-archipelago42','round-n-aborea73','round-n-lakes17')){Prune-Evidence (Safe-Child $localHistory $dir)}
foreach($dir in @('fresh-w2-earth200','fresh-w2-cp200','stopping-w-earth500','stopping-w-cp500','living-w2-browser','unity-w2-dock')){Prune-Evidence (Safe-Child $localHistory $dir)}
Prune-Evidence (Safe-Child $localHistory 'fresh-w2-cp200-maxtech') @('final.world','ready.world')
foreach($file in Get-ChildItem -LiteralPath $localHistory -File){if($file.Extension-eq'.world'-and$file.Name-ne'arc42-k-prestate300.world'){Remove-ReviewItem $file.FullName}}
Prune-Directories (Safe-Child $localRoot 'history31') @('earth-release')
Prune-Evidence (Safe-Child $localRoot 'history31/earth-release')
Prune-Directories $externalRoot @('artifacts','unity-cache','experiments','history32-v-earth200-replay','history32-v-cp200-replay','history32-provisioned-port-ui','history32-w-suppliers-earth500','history32-w-suppliers-iceage500')
Prune-Directories (Safe-Child $externalRoot 'experiments') @('monsoon-333')
Prune-Directories (Safe-Child $externalRoot 'artifacts') @('history32')
$externalHistory=Safe-Child $externalRoot 'artifacts/history32'
Prune-Directories $externalHistory @('fresh-o-continents200','round-v-pangeae31415','round-v-iceage27182','round-q-b2-aborea73-replay','receiver-learning-ab','regional-delivery-ab','living-v-browser','unity-u2-cp-performance1500','unity-v-cp-performance1500','unity-v-il2cpp-cp-performance1500','unity-v-il2cpp-dock','unity-w-cache-cp-performance1500','unity-w2-fresh-cp-performance1000','unity-w2-dense-v-cp-performance1500','unity-w2-complete-cp-performance1000')
foreach($dir in @('fresh-o-continents200','round-v-pangeae31415','round-v-iceage27182','round-q-b2-aborea73-replay','living-v-browser','unity-u2-cp-performance1500','unity-v-cp-performance1500','unity-v-il2cpp-cp-performance1500','unity-v-il2cpp-dock')){Prune-Evidence (Safe-Child $externalHistory $dir)}
foreach($dir in @('unity-w-cache-cp-performance1500','unity-w2-fresh-cp-performance1000','unity-w2-dense-v-cp-performance1500','unity-w2-complete-cp-performance1000')){Prune-Evidence (Safe-Child $externalHistory $dir)}
Prune-Evidence (Safe-Child $externalHistory 'receiver-learning-ab') @('baseline500.world','enabled500.world')
Prune-Evidence (Safe-Child $externalHistory 'regional-delivery-ab') @('delivery500.world')
foreach($dir in @('history32-v-earth200-replay','history32-v-cp200-replay')){Prune-Evidence (Safe-Child $externalRoot $dir)}
Prune-Evidence (Safe-Child $externalRoot 'history32-provisioned-port-ui') @('controlled-v-archipelago.world')
$total=($report|Measure-Object Bytes -Sum).Sum
$result=[ordered]@{Applied=[bool]$Apply;DeletedLogicalGiB=[math]::Round($total/1GB,3);Items=$report.Count;BeforeFreeBytes=$before;Entries=$report.ToArray()}
$reportPath=Join-Path $localRoot 'cleanup-latest.json'
if($Apply){$result|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $reportPath -Encoding UTF8}
[pscustomobject]@{Apply=[bool]$Apply;LogicalGiB=[math]::Round($total/1GB,3);Items=$report.Count;Report=$(if($Apply){$reportPath}else{'Dry run; nothing deleted'})}|Format-List
