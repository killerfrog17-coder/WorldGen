param(
    [string]$Unity='C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe',
    [switch]$BuildPlayer,
    [switch]$DevelopmentPlayer,
    [switch]$ScriptsOnly,
    [switch]$PerformancePlayer,
    [switch]$Il2Cpp, # Explicit major-release build; normal iteration uses Mono.
    [switch]$PlayerCheck,
    [switch]$Isolated,
    [string]$PlayerFolder=""
)
$ErrorActionPreference='Stop'
if($BuildPlayer -and $Isolated){throw 'Full isolated player copies are retired. Build in the inactive reusable slot; isolated source validation remains available.'}
$repoRoot=Split-Path -Parent $PSScriptRoot
if($BuildPlayer){
    python (Join-Path $repoRoot 'scripts/audit-project-layout.py')
    if($LASTEXITCODE -ne 0){throw 'A permanent input depends on disposable scratch; repair the project layout before building.'}
}
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
if($BuildPlayer){
    # Permanent players are outside disposable scratch. Reuse two slots forever.
    $playerSlots=@('slot-a','slot-b')
    $selectedPlayer=''
    $playerShortcut=Join-Path $repoRoot 'WorldGen.exe - Shortcut.lnk'
    if(Test-Path -LiteralPath $playerShortcut){$selectedPlayer=(New-Object -ComObject WScript.Shell).CreateShortcut($playerShortcut).TargetPath}
    $selectedSlot=if($selectedPlayer){Split-Path -Leaf (Split-Path -Parent $selectedPlayer)}else{''}
    if(!$PlayerFolder){$PlayerFolder=if($selectedSlot -eq $playerSlots[0]){$playerSlots[1]}else{$playerSlots[0]}}
    if($PlayerFolder -notin $playerSlots){throw 'Per-update player folders are retired. Omit -PlayerFolder to reuse the inactive fixed slot.'}
    if($PlayerFolder -eq $selectedSlot){throw 'Refusing to overwrite the selected playable game. Omit -PlayerFolder to build in the other reusable slot.'}
    # A launcher can be switched while the previous game is still open. That
    # older process also protects its slot until the owner exits it normally.
    $runningSlotDirectory=[IO.Path]::GetFullPath((Join-Path $repoRoot ('builds/'+$PlayerFolder)))
    $runningSlotPaths=@($runningSlotDirectory)
    if(Test-Path -LiteralPath $runningSlotDirectory){
        $runningSlotItem=Get-Item -LiteralPath $runningSlotDirectory
        if($runningSlotItem.LinkType -eq 'Junction'){$runningSlotPaths+=@($runningSlotItem.Target | ForEach-Object {[IO.Path]::GetFullPath($_)})}
    }
    $runningSlotPlayer=Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'" | Where-Object {
        $_.ExecutablePath -and [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($_.ExecutablePath)) -in $runningSlotPaths
    }
    if($runningSlotPlayer){throw ('The owner still has '+$PlayerFolder+' running. Leave it untouched; wait until the owner exits it before reusing this slot.')}
    $oldStamp=Join-Path $runningSlotDirectory 'build-inputs.json'
    if($ScriptsOnly){
        if($Il2Cpp -or !(Test-Path -LiteralPath $oldStamp)){throw 'Scripts-only iteration requires an existing managed development player in this inactive slot.'}
        $priorBuild=Get-Content -Raw -LiteralPath $oldStamp|ConvertFrom-Json
        if($priorBuild.Backend -ne 'Mono' -or !$priorBuild.Development){throw 'Scripts-only iteration requires an existing managed development player.'}
    }
    if(Test-Path -LiteralPath $oldStamp){Remove-Item -LiteralPath $oldStamp -Force}
    Write-Output ('Reusing player workspace: '+$PlayerFolder)
}
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
    $motionSource=Join-Path $repoRoot 'assets/fauna-motion'
    if(Test-Path -LiteralPath (Join-Path $motionSource 'index.json')){
        $motionTarget=Join-Path $project 'Assets/StreamingAssets/fauna-motion'
        New-Item -ItemType Directory -Force -Path $motionTarget | Out-Null
        foreach($motion in (Get-Content -Raw -LiteralPath (Join-Path $motionSource 'index.json')|ConvertFrom-Json).animals){
            $motionFile=Join-Path $motionSource $motion.file
            if((Get-FileHash -LiteralPath $motionFile).Hash.ToLowerInvariant() -ne $motion.sha256){throw "Animal motion hash mismatch: $($motion.slug)"}
            Copy-Item -LiteralPath $motionFile -Destination $motionTarget -Force
        }
        Copy-Item -LiteralPath (Join-Path $motionSource 'index.json') -Destination $motionTarget -Force
    }
}
$method=if($BuildPlayer){'WorldGenBatch.BuildPlayer'}else{'WorldGenBatch.Validate'}
$log=Join-Path $artifacts $(if($BuildPlayer){'unity-build.log'}else{'unity-validation.log'})
$buildRoot=Join-Path $repoRoot 'builds'
$playerOutput=[IO.Path]::GetFullPath((Join-Path $buildRoot ($PlayerFolder+'/WorldGen.exe')))
if(!$playerOutput.StartsWith([IO.Path]::GetFullPath($buildRoot)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Player folder must stay inside the permanent build root'}
$arguments=@('-batchmode' ,'-nographics','-quit','-projectPath',('"'+$project+'"'),'-executeMethod',$method,'--worldgen-build-output',('"'+$playerOutput+'"'),'-logFile',('"'+$log+'"'))
if($DevelopmentPlayer -or ($BuildPlayer -and !$Il2Cpp)){$arguments+='--worldgen-development-player'}
if($PerformancePlayer){$arguments+='--worldgen-performance-build'}
if($ScriptsOnly){if(!$BuildPlayer){throw '-ScriptsOnly requires -BuildPlayer.'};$arguments+='--worldgen-scripts-only'}
if($Il2Cpp -and !$BuildPlayer){throw '-Il2Cpp is a player-build option.'}
if($BuildPlayer -and $Il2Cpp){$arguments+='--worldgen-il2cpp-player'}
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
    [ordered]@{BuiltUtc=[DateTime]::UtcNow.ToString('o');Backend=$(if($Il2Cpp){'IL2CPP'}else{'Mono'});Development=[bool]($DevelopmentPlayer -or !$Il2Cpp);ScriptsOnly=[bool]$ScriptsOnly;Inputs=$assemblyInputs}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($playerOutput)) 'build-inputs.json') -Encoding UTF8
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
