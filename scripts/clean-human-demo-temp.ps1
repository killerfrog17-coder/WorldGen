$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$demoRoot = (Resolve-Path (Join-Path $repoRoot 'artifacts/human-builder-demo')).Path
$umaRoot = (Resolve-Path (Join-Path $repoRoot 'artifacts/tools/UMA/UMAProject')).Path
$demoProcesses = @(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'Unity.exe' -and $_.CommandLine.Replace('/','\') -like "*$umaRoot*"
})
if ($demoProcesses.Count) { throw 'The isolated UMA project is still running; leave its cache alone.' }
$targets = [System.Collections.Generic.List[string]]::new()
foreach ($name in @('makehuman_system_assets_cc0.zip','skins01_cc0.zip','skins02_cc0.zip','hair01_cc0.zip',
    'MPFB - Diagnostic.blend','MPFB - Diagnostic.blend1','MPFB - Appearance Candidates.blend1',
    'Human - Character Builder Comparison.blend1','uma/probe',
    'assetpacks.html','hair01.html','skins01.html','skins02.html','makehuman_system_assets.html',
    'system-zip-tail.bin','mpfb2-tree.json','UMA-tree.json','makehuman-tree.json',
    'DynamicCharacterAvatar.cs','UMADefaultUtilityEnvironment.prefab','UMAExport.cs','UMAGeneratorBase.cs',
    'mpfb2-docs_services_humanservice.md','mpfb2-README.md','mpfb2-src_mpfb_services_humanservice.py',
    'UMA-UMAProject_Packages_manifest.json')) {
    $targets.Add((Join-Path $demoRoot $name))
}
$targets.Add((Join-Path $umaRoot 'Library'))
$targets.Add((Join-Path $umaRoot 'Temp'))
$used = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($code in @('A','B','C','D','E','F')) {
    $folder = Join-Path $demoRoot "uma/$code"
    $character = Get-Content -LiteralPath (Join-Path $folder 'character.json') -Raw | ConvertFrom-Json
    foreach ($mesh in $character.meshes) { foreach ($material in $mesh.materials) { foreach ($map in $material.maps) {
        [void]$used.Add([IO.Path]::GetFullPath((Join-Path $folder $map.path)))
    } } }
}
Get-ChildItem -LiteralPath (Join-Path $demoRoot 'uma/textures') -File | ForEach-Object {
    if (-not $used.Contains($_.FullName)) { $targets.Add($_.FullName) }
}
$removed = @()
foreach ($candidate in $targets) {
    if (-not (Test-Path -LiteralPath $candidate)) { continue }
    $absolute = [IO.Path]::GetFullPath($candidate)
    $insideDemo = $absolute.StartsWith($demoRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)
    $explicitCache = $absolute -eq (Join-Path $umaRoot 'Library') -or $absolute -eq (Join-Path $umaRoot 'Temp')
    if (-not ($insideDemo -or $explicitCache)) { throw "Unsafe cleanup path: $absolute" }
    $item = Get-Item -LiteralPath $absolute
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing link: $absolute" }
    if ($item.PSIsContainer -and @(Get-ChildItem -LiteralPath $absolute -Recurse -Attributes ReparsePoint).Count) {
        throw "Refusing tree containing filesystem links: $absolute"
    }
    $bytes = if ($item.PSIsContainer) {
        [long](Get-ChildItem -LiteralPath $absolute -File -Recurse | Measure-Object Length -Sum).Sum
    } else { [long]$item.Length }
    if ($item.PSIsContainer) { Remove-Item -LiteralPath $absolute -Recurse -Force }
    else { Remove-Item -LiteralPath $absolute -Force }
    $removed += [pscustomobject]@{path=$absolute;bytes=$bytes}
}
$report = [pscustomobject]@{removed=$removed;bytes=[long]($removed | Measure-Object bytes -Sum).Sum}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $demoRoot 'temp-cleanup.json')
Write-Output "Removed $($report.bytes) bytes of this demo's temporary data."
