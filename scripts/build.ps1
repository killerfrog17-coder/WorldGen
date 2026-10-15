param([switch]$SkipUnity)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    dotnet build WorldGen.sln -c Release --nologo
    if($LASTEXITCODE -ne 0){throw 'Solution build failed'}
    & (Join-Path $PSScriptRoot 'prepare-world-library.ps1')
    $workerSource=Join-Path $repoRoot 'src/WorldGen.Web.Worker/bin/Release/net10.0/wwwroot'
    $workerDestination=Join-Path $repoRoot 'src/WorldGen.Web/wwwroot/worker'
    $resolvedWorkerDestination=[IO.Path]::GetFullPath($workerDestination)
    if($resolvedWorkerDestination -ne [IO.Path]::GetFullPath((Join-Path $repoRoot 'src/WorldGen.Web/wwwroot/worker'))){throw 'Unexpected generated worker path'}
    if(Test-Path -LiteralPath $resolvedWorkerDestination){Remove-Item -LiteralPath $resolvedWorkerDestination -Recurse -Force}
    New-Item -ItemType Directory -Force $workerDestination | Out-Null
    Copy-Item -Path "$workerSource/*" -Destination $workerDestination -Recurse -Force
    Copy-Item -LiteralPath 'src/WorldGen.Web.Worker/wwwroot/main.js' -Destination $workerDestination -Force
    & (Join-Path $PSScriptRoot 'prepare-music.ps1')
    dotnet build src/WorldGen.Web -c Release --nologo
    if($LASTEXITCODE -ne 0){throw 'Browser build failed'}
    if(!$SkipUnity){
        $pluginDestination=Join-Path $repoRoot 'unity/WorldGen.Unity/Assets/Plugins/WorldGen'
        New-Item -ItemType Directory -Force $pluginDestination | Out-Null
        foreach($name in @('Core','Serialization','Presentation')){
            Copy-Item -LiteralPath "src/WorldGen.$name/bin/Release/netstandard2.1/WorldGen.$name.dll" -Destination $pluginDestination -Force
        }
        Copy-Item -LiteralPath 'src/WorldGen.Core/bin/Release/netstandard2.1/DelaunatorSharp.dll' -Destination $pluginDestination -Force
    }
} finally {Pop-Location}
