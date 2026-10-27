$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskRoot = (Resolve-Path (Join-Path $repoRoot 'artifacts/fauna-eye-closure-repair')).Path
$receipt = Get-Content -LiteralPath (Join-Path $taskRoot 'installation.json') -Raw | ConvertFrom-Json
if ($receipt.count -ne 177) { throw 'A complete verified installation is required before cleanup.' }
$canonical = [IO.Path]::GetFullPath($receipt.canonical)
$expected = [IO.Path]::GetFullPath((Join-Path $repoRoot 'unity/WorldGen.Unity/Assets/StreamingAssets/fauna'))
if ($canonical -ne $expected) { throw 'Unexpected canonical asset path.' }
foreach ($animal in $receipt.animals) {
    $bundle = Join-Path $canonical ($animal.slug + '.fauna')
    if ((Get-FileHash -LiteralPath $bundle).Hash.ToLowerInvariant() -ne $animal.bundle_sha256) { throw "Installed bundle changed: $bundle" }
}
if (Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Unity.exe' -and $_.CommandLine.Replace('/','\').Contains($taskRoot) }) { throw 'Own isolated converter is still running.' }
$removed = @()
foreach ($relative in @('bundle-project/Library','bundle-project/Temp','bundle-project/Assets/WorldGen/Editor/GeneratedFauna','before','native-before','bundles')) {
    $target = [IO.Path]::GetFullPath((Join-Path $taskRoot $relative))
    if (-not $target.StartsWith($taskRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe target: $target" }
    if (-not (Test-Path -LiteralPath $target)) { continue }
    $item = Get-Item -LiteralPath $target
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing link: $target" }
    if (@(Get-ChildItem -LiteralPath $target -Recurse -Attributes ReparsePoint).Count) { throw "Refusing linked tree: $target" }
    $bytes = [long](Get-ChildItem -LiteralPath $target -Recurse -File | Measure-Object Length -Sum).Sum
    Remove-Item -LiteralPath $target -Recurse -Force
    $removed += [pscustomobject]@{path=$target;bytes=$bytes}
}
[pscustomobject]@{removed=$removed;bytes=[long]($removed | Measure-Object bytes -Sum).Sum} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskRoot 'cache-cleanup.json')
Write-Output 'Removed only verified repair cache/staging duplicates; canonical assets and player data retained.'
