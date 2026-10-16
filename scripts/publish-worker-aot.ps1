param(
    [string]$OutputDirectory,
    [string]$BuildArtifactsDirectory
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=Join-Path $repoRoot 'artifacts/browser-worker-aot'}
if(-not $BuildArtifactsDirectory){$BuildArtifactsDirectory=Join-Path $repoRoot 'artifacts/browser-worker-aot-build'}
$publishRoot=[IO.Path]::GetFullPath($OutputDirectory)
$buildRoot=[IO.Path]::GetFullPath($BuildArtifactsDirectory)
if($publishRoot -eq $repoRoot -or $buildRoot -eq $repoRoot -or $publishRoot -eq $buildRoot){throw 'Choose distinct, dedicated publish and build-artifact directories, not the repository root.'}
Push-Location $repoRoot
try {
    $timer=[Diagnostics.Stopwatch]::StartNew()
    $revision=(git rev-parse HEAD).Trim()
    $sourceDirty=[bool](git status --porcelain)
    dotnet publish src/WorldGen.Web.Worker/WorldGen.Web.Worker.csproj -c Release --artifacts-path $buildRoot -o $publishRoot -p:WorldGenWorkerAot=true --nologo
    if($LASTEXITCODE -ne 0){throw 'Optional AOT worker publication failed. The ordinary worker was not replaced.'}
    $webRoot=Join-Path $publishRoot 'wwwroot'
    Copy-Item -LiteralPath (Join-Path $repoRoot 'src/WorldGen.Web.Worker/wwwroot/main.js') -Destination (Join-Path $webRoot 'main.js') -Force
    $framework=Join-Path $webRoot '_framework'
    $rawBytes=(Get-ChildItem -LiteralPath $framework -File -Filter '*.wasm'|Measure-Object Length -Sum).Sum
    $gzipBytes=(Get-ChildItem -LiteralPath $framework -File -Filter '*.gz'|Measure-Object Length -Sum).Sum
    $inputs=@()
    foreach($name in @('Core','Serialization','Presentation','Web.Worker')){
        $inputDll=Join-Path $buildRoot "bin/WorldGen.$name/release/WorldGen.$name.dll"
        $inputs+=[ordered]@{Assembly="WorldGen.$name";Sha256=(Get-FileHash -LiteralPath $inputDll -Algorithm SHA256).Hash.ToLowerInvariant()}
    }
    $report=[ordered]@{
        SourceCommit=$revision
        WorkingTreeDirty=$sourceDirty
        HeadChangedDuringPublish=((git rev-parse HEAD).Trim() -ne $revision)
        PublishedUtc=[DateTime]::UtcNow.ToString('o')
        ElapsedSeconds=[Math]::Round($timer.Elapsed.TotalSeconds,2)
        BuildArtifactsDirectory=$buildRoot
        WebRoot=$webRoot
        FrameworkWasmBytes=$rawBytes
        FrameworkGzipBytes=$gzipBytes
        CompiledInputAssemblies=$inputs
        RetainsAuthoritativeAssembliesAndIL=$true
    }
    $report|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $publishRoot 'aot-publication.json') -Encoding UTF8
    Write-Host ('AOT worker published in {0:n1}s. Existing development/browser assets were not replaced.' -f $timer.Elapsed.TotalSeconds)
    Write-Host "Test with WORLDGEN_WORKER_ROOT=$webRoot and scripts/worker-check.mjs or scripts/living-world-browser-check.mjs."
    Write-Host 'The dedicated build-artifact directory is a disposable compiler cache, not a project or save backup.'
} finally {Pop-Location}
