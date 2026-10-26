$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$previewRoot = (Resolve-Path (Join-Path $repoRoot 'artifacts/human-animation-preview')).Path
$allowed = @('MPFB - Motion Probe.blend','probe-renders','probe-evidence.json','probe-sheet.jpg')
$removed = @()
foreach ($name in $allowed) {
    $target = [IO.Path]::GetFullPath((Join-Path $previewRoot $name))
    if (-not $target.StartsWith($previewRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe target: $target" }
    if (-not (Test-Path -LiteralPath $target)) { continue }
    $item = Get-Item -LiteralPath $target
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing link: $target" }
    if ($item.PSIsContainer -and @(Get-ChildItem -LiteralPath $target -Recurse -Attributes ReparsePoint).Count) { throw "Refusing links in tree: $target" }
    $bytes = if ($item.PSIsContainer) { [long](Get-ChildItem -LiteralPath $target -Recurse -File | Measure-Object Length -Sum).Sum } else { [long]$item.Length }
    if ($item.PSIsContainer) { Remove-Item -LiteralPath $target -Recurse -Force } else { Remove-Item -LiteralPath $target -Force }
    $removed += [pscustomobject]@{path=$target;bytes=$bytes}
}
[pscustomobject]@{removed=$removed;bytes=[long]($removed | Measure-Object bytes -Sum).Sum} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $previewRoot 'temp-cleanup.json')
Write-Output "Removed only obsolete animation probe data."
