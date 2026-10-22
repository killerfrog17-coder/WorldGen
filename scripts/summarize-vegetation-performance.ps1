param([string]$Evidence='artifacts/performance-review')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$directory=[IO.Path]::GetFullPath((Join-Path $repo $Evidence))
$rows=Import-Csv -LiteralPath (Join-Path $directory 'summary.csv')
function Reference($siteRows){
    $reference=$siteRows | Where-Object {$_.case -eq 'full' -and $_.settled -eq 'True'} | Select-Object -First 1
    if(!$reference){$reference=$siteRows | Where-Object {$_.case -eq 'full-repeat' -and $_.settled -eq 'True'} | Select-Object -First 1}
    return $reference
}
$report=[Collections.Generic.List[string]]::new()
$report.Add('# Rendering performance measurements')
$report.Add('')
$report.Add('Uncapped offscreen D3D11/URP camera rendering (resolution in machine.txt; default 1920x1080), fixed seed/sites, scale 0.1. These are not fullscreen/VSync or whole-game guarantees. Clear noon; history and music held; F3 hidden. Grass 30m close/300m total; High terrain, 60km view, 153.6m minimum full-detail radius; non-grass defaults unless named. Each row is a separate interval; costs are not additive. See machine.txt, sites.txt, equivalence.txt and player.log.')
$report.Add('')
$report.Add('| Site | Full FPS | Original CPU paths | No grass | No specialists | No mid | No trees | Terrain/sky only | Full p99 ms | Turning FPS | Travel FPS |')
$report.Add('|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|')
foreach($group in ($rows | Group-Object site)){
    $map=@{};foreach($row in $group.Group){$map[$row.case]=$row};$ref=Reference $group.Group
    if($ref){$map['full']=$ref}else{$map.Remove('full')}
    function Fps([string]$key){if($map.ContainsKey($key)){if($map[$key].settled -ne 'True' -and $key -ne 'travel'){return 'loading'};return ([double]$map[$key].fps).ToString('F1',[Globalization.CultureInfo]::InvariantCulture)};return '-'}
    $p99=if($map.ContainsKey('full')){([double]$map['full'].p99_ms).ToString('F2',[Globalization.CultureInfo]::InvariantCulture)}else{'-'}
    $report.Add('| '+$group.Name+' | '+((@((Fps 'full'),(Fps 'original-cpu'),(Fps 'no-grass'),(Fps 'no-specialists'),(Fps 'no-mid'),(Fps 'no-trees'),(Fps 'terrain-only'),$p99,(Fps 'turning'),(Fps 'travel'))) -join ' | ')+' |')
}
$report.Add('')
$report.Add('## All settings and ablations')
$report.Add('')
$report.Add('| Site | Case | FPS | Change vs full | Frame-time saving ms | p99 ms | Max ms | Settled at end |')
$report.Add('|---|---|---:|---:|---:|---:|---:|---|')
foreach($row in $rows){
    $full=Reference ($rows | Where-Object {$_.site -eq $row.site})
    $delta=if($full){'{0:+0.0;-0.0;0.0}%' -f (100*([double]$row.fps/[double]$full.fps-1))}else{'-'}
    $saved=if($full){'{0:F3}' -f (1000/[double]$full.fps-1000/[double]$row.fps)}else{'-'}
    $report.Add(('| {0} | {1} | {2:F1} | {3} | {4} | {5:F2} | {6:F2} | {7} |' -f $row.site,$row.case,[double]$row.fps,$delta,$saved,[double]$row.p99_ms,[double]$row.max_ms,$row.settled))
}
$report.Add('')
$report.Add('Frame-time saving = full average milliseconds minus selected-case average milliseconds. A positive value is faster. A negative value can reflect newly exposed pixels, different views, CPU/GPU interactions or run variance. Turning/travel/flying are different workloads, not clean layer ablations. full-repeat checks drift. Unsettled rows include streaming and must not be treated as settled comparisons. gpu_ms=0 with timing_samples=0 means UNAVAILABLE, not a free GPU frame.')
$report.Add('The overview uses settled full-repeat if the first full interval was still loading. If neither is settled, it has no accepted full baseline.')
$report | Out-File -LiteralPath (Join-Path $directory 'report.md') -Encoding utf8
Write-Output (Join-Path $directory 'report.md')
