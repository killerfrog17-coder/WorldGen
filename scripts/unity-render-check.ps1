param([switch]$Isolated, [int]$Detail=204000, [string]$PlayerFolder="player-final")
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$validationRoot=if($Isolated){Join-Path $repoRoot 'artifacts/unity-v2'}else{$repoRoot}
$player=Join-Path $validationRoot ('artifacts/'+$PlayerFolder+'/WorldGen.exe')
if(!(Test-Path -LiteralPath $player)){throw 'Build the Unity player before running render checks.'}
$output=Join-Path $repoRoot 'artifacts/unity-renders-v3'
New-Item -ItemType Directory -Force $output | Out-Null
foreach($projection in @('Globe','Map')){
    foreach($layer in @('Terrain','Satellite','Climate','Heightmap')){
        $name=($projection+'-'+$layer).ToLowerInvariant()
        $image=Join-Path $output ($name+'.png')
        $log=Join-Path $output ($name+'.log')
        $arguments=@('-force-d3d11','-screen-width','1600','-screen-height','1000','-screen-fullscreen','0','--worldgen-view',$projection,'--worldgen-layer',$layer,'--worldgen-detail',$Detail,'--worldgen-screenshot',('"'+$image+'"'),'-logFile',('"'+$log+'"'))
        $run=Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $run.WaitForExit()
        if($run.ExitCode -ne 0 -or !(Test-Path -LiteralPath $image) -or (Get-Item -LiteralPath $image).Length -lt 5000){throw "Render failed: $name. Inspect $log"}
        Write-Host "PASS $name $Detail terrain cells"
    }
}
