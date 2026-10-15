param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
if ((Get-Item -LiteralPath $artifactRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'The artifacts root must be a real project directory.'
}
# Keep the installed player, latest review, training checkpoints and small
# provenance/rollback records. Never traverse links into other drives.
$keep = @('player-history-18-realism', 'history18-review', 'climate-learning',
    'orogen-reference', 'pre-complex-goldens', 'pre-interconnectedness-goldens',
    'pre-interconnectedness-core', 'pre-tectonic-realism')
$targets = @()
$bytes = 0L
$trackedArtifacts = @(git -C $projectRoot ls-files -- artifacts)
if ($LASTEXITCODE -ne 0) { throw 'Could not verify tracked artifact paths.' }
$runningPaths = @(Get-CimInstance Win32_Process | ForEach-Object { $_.ExecutablePath } | Where-Object { $_ })
foreach ($entry in Get-ChildItem -LiteralPath $artifactRoot -Force) {
    if ($entry.Name -in $keep) { continue }
    if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        Write-Output "Retaining external link: $($entry.Name)"
        continue
    }
    $target = [IO.Path]::GetFullPath($entry.FullName)
    if ([IO.Path]::GetDirectoryName($target) -ne $artifactRoot -or
        -not $target.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe target: $target" }
    $relative = $target.Substring($projectRoot.Length + 1).Replace('\', '/')
    $tracked = @($trackedArtifacts | Where-Object { $_ -eq $relative -or $_.StartsWith($relative + '/') })
    if ($tracked.Count -ne 0) { throw "Tracked target: $target" }
    $running = @($runningPaths | Where-Object {
        $_.StartsWith($target + '\', [StringComparison]::OrdinalIgnoreCase)
    })
    if ($running.Count) { Write-Output "Retaining running build: $target"; continue }
    $stack = [Collections.Generic.Stack[IO.FileSystemInfo]]::new()
    $stack.Push($entry)
    while ($stack.Count) {
        $item = $stack.Pop()
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Nested directory link needs manual review: $($item.FullName)"
        }
        if ($item -is [IO.DirectoryInfo]) {
            foreach ($child in $item.GetFileSystemInfos()) { $stack.Push($child) }
        } else { $bytes += $item.Length }
    }
    $targets += $target
}
Write-Output ("Validated {0} obsolete artifact entries, {1:N2} GiB." -f $targets.Count, ($bytes / 1GB))
if (-not $Apply) { Write-Output 'Preview only. Pass -Apply to permanently remove these ignored outputs.'; return }
foreach ($target in $targets) {
    Remove-Item -LiteralPath $target -Recurse -Force
}
Write-Output ("Removed {0} obsolete artifact entries. Source, saves, current player and retained evidence are untouched." -f $targets.Count)
