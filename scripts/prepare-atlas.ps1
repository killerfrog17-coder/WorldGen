$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$source=Join-Path $repoRoot 'assets/atlas/biomes'
$destinations=@('src/WorldGen.Web/wwwroot/atlas/biomes','unity/WorldGen.Unity/Assets/StreamingAssets/atlas/biomes')
foreach($relative in $destinations){
    $destination=Join-Path $repoRoot $relative
    New-Item -ItemType Directory -Force $destination | Out-Null
    Get-ChildItem -LiteralPath $source -Filter '*.jpg' -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $destination -Force}
}
