param([int]$Detail=80000,[int]$BrowserDetail=20000)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $bundle=Join-Path $repoRoot 'unity/WorldGen.Unity/Assets/StreamingAssets/world/Earth.world'
    dotnet run --project src/WorldGen.Cli -c Release -- --preset Earth --detail $Detail --save-world $bundle --install-earth-library (Join-Path $repoRoot 'world') --summary
    if($LASTEXITCODE -ne 0){throw 'Bundled Earth snapshot generation failed'}
    $webWorld=Join-Path $repoRoot 'src/WorldGen.Web/wwwroot/world'
    New-Item -ItemType Directory -Force $webWorld | Out-Null
    # Browser interpretation/deserialization is slower; keep its starter world
    # smaller. Both are real snapshots from the same authoritative pipeline.
    dotnet run --project src/WorldGen.Cli -c Release --no-build -- --preset Earth --detail $BrowserDetail --save-world (Join-Path $webWorld 'Earth.world') --summary
    if($LASTEXITCODE -ne 0){throw 'Browser Earth snapshot generation failed'}
    Write-Host "Prepared full Earth snapshot: $bundle"
} finally {Pop-Location}
