param(
    [string]$Unity='C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe',
    [switch]$BuildPlayer,
    [switch]$DevelopmentPlayer,
    [switch]$PerformancePlayer,
    [switch]$Il2Cpp,
    [switch]$PlayerCheck,
    [switch]$Isolated,
    [string]$PlayerFolder="player"
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$sourceProject=Join-Path $repoRoot 'unity/WorldGen.Unity'
$validationRoot=if($Isolated){Join-Path $repoRoot 'artifacts/unity-v2'}else{$repoRoot}
$project=Join-Path $validationRoot 'unity/WorldGen.Unity'
$normalizedProject=([IO.Path]::GetFullPath($project)).Replace('\','/')
$open=Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and !$_.CommandLine.Contains('-adb2') -and $_.CommandLine.Replace('\','/').IndexOf($normalizedProject,[StringComparison]::OrdinalIgnoreCase) -ge 0
}
if($open){throw "An editor already has this project open. Close that editor, or use -Isolated when validating the main project."}
if($Isolated){
    New-Item -ItemType Directory -Force $project | Out-Null
    foreach($part in @('Assets','Packages','ProjectSettings')){
        robocopy (Join-Path $sourceProject $part) (Join-Path $project $part) /E /NFL /NDL /NJH /NJS /NP | Out-Null
        if($LASTEXITCODE -gt 7){throw "Failed to copy $part into the isolated project"}
    }
    $goldenDestination=Join-Path $validationRoot 'tests/golden/v3'
    New-Item -ItemType Directory -Force $goldenDestination | Out-Null
    Copy-Item -Path (Join-Path $repoRoot 'tests/golden/v3/*.json') -Destination $goldenDestination -Force
}
$artifacts=Join-Path $validationRoot 'artifacts'
New-Item -ItemType Directory -Force $artifacts | Out-Null
$workerOutput=Join-Path $repoRoot 'artifacts/terrain-worker'
if($BuildPlayer){
    dotnet publish (Join-Path $repoRoot 'src/WorldGen.Cli') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $workerOutput --nologo
    if($LASTEXITCODE -ne 0){throw 'Terrain worker publish failed'}
    # Freeze the same authoritative assembly inputs for worker and IL2CPP.
    foreach($name in @('Core','Serialization','Presentation')){Copy-Item -LiteralPath (Join-Path $repoRoot "src/WorldGen.$name/bin/Release/netstandard2.1/WorldGen.$name.dll") -Destination (Join-Path $project 'Assets/Plugins/WorldGen') -Force}
    # A clean checkout has no ignored plugin DLLs. Restore the pinned NuGet
    # dependency alongside our assemblies rather than relying on an old editor
    # cache to have supplied this fourth runtime assembly.
    Copy-Item -LiteralPath (Join-Path $repoRoot 'src/WorldGen.Core/bin/Release/netstandard2.1/DelaunatorSharp.dll') -Destination (Join-Path $project 'Assets/Plugins/WorldGen') -Force
}
$method=if($BuildPlayer){'WorldGenBatch.BuildPlayer'}else{'WorldGenBatch.Validate'}
$log=Join-Path $artifacts $(if($BuildPlayer){'unity-build.log'}else{'unity-validation.log'})
$playerOutput=[IO.Path]::GetFullPath((Join-Path $artifacts ($PlayerFolder+'/WorldGen.exe')))
if(!$playerOutput.StartsWith([IO.Path]::GetFullPath($artifacts)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Player folder must stay inside validation artifacts'}
$arguments=@('-batchmode' ,'-nographics','-quit','-projectPath',('"'+$project+'"'),'-executeMethod',$method,'--worldgen-build-output',('"'+$playerOutput+'"'),'-logFile',('"'+$log+'"'))
if($DevelopmentPlayer){$arguments+='--worldgen-development-player'}
if($PerformancePlayer){$arguments+='--worldgen-performance-build'}
if($Il2Cpp){if(!$BuildPlayer){throw '-Il2Cpp is a player-build option.'};$arguments+='--worldgen-il2cpp-player'}
$assemblyInputs=@()
if($BuildPlayer){foreach($name in @('WorldGen.Core','WorldGen.Serialization','WorldGen.Presentation','DelaunatorSharp')){$inputDll=Join-Path $project ('Assets/Plugins/WorldGen/'+$name+'.dll');$assemblyInputs+=[ordered]@{Assembly=$name;Path=$inputDll;Sha256=(Get-FileHash -LiteralPath $inputDll).Hash.ToLowerInvariant()}}}
$run=Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$run.WaitForExit()
if($run.ExitCode -ne 0){throw "Unity failed. Inspect $log"}
if(Select-String -LiteralPath $log -Pattern 'Shader error in ' -Quiet){throw "Unity reported a shader compilation error despite a successful exit. Inspect $log"}
if($BuildPlayer){
    foreach($inputAssembly in $assemblyInputs){if((Get-FileHash -LiteralPath $inputAssembly.Path).Hash.ToLowerInvariant() -ne $inputAssembly.Sha256){throw 'A staged authoritative assembly changed during the player build; do not accept this output.'}}
    $packagedWorker=Join-Path ([IO.Path]::GetDirectoryName($playerOutput)) 'TerrainWorker'
    New-Item -ItemType Directory -Path $packagedWorker -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $workerOutput 'WorldGen.Cli.exe') -Destination $packagedWorker -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot 'src/WorldGen.Core/Data/earth-etopo2022-10min.source.json') -Destination (Join-Path ([IO.Path]::GetDirectoryName($playerOutput)) 'EARTH-DATA.json') -Force
    [ordered]@{BuiltUtc=[DateTime]::UtcNow.ToString('o');Backend=$(if($Il2Cpp){'IL2CPP'}else{'Project default'});Development=[bool]$DevelopmentPlayer;Inputs=$assemblyInputs}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($playerOutput)) 'build-inputs.json') -Encoding UTF8
}
if($PlayerCheck){
    $output=Join-Path $artifacts 'unity-player'
    $player=Start-Process -FilePath $playerOutput -ArgumentList @('-batchmode','-nographics','--worldgen-check','--worldgen-output',('"'+$output+'"'),'-logFile',('"'+(Join-Path $artifacts 'player-check.log')+'"')) -WindowStyle Hidden -PassThru
    $player.WaitForExit()
    if($player.ExitCode -ne 0){throw 'Player checks failed'}
    foreach($golden in Get-ChildItem (Join-Path $repoRoot 'tests/golden/v3') -Filter '*.hashes.json'){
        $expected=Get-Content -Raw -LiteralPath $golden.FullName | ConvertFrom-Json
        $actual=Get-Content -Raw -LiteralPath (Join-Path $output $golden.Name) | ConvertFrom-Json
        if(($expected.Hash -join ',') -ne ($actual.Hash -join ',')){throw "Player hash mismatch: $($golden.Name)"}
    }
    Write-Host 'All player stage hashes match CLI golden corpus.'
}
