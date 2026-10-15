param([int]$Iterations = 0, [string]$Resume = '', [switch]$PrepareOnly, [switch]$Aggressive)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    Write-Host 'WorldGen: bounded Earth climate learning (experimental)'
    Write-Host 'Leaves current defaults and all existing saves untouched. Creates comparison worlds.'
    $runRoot = Join-Path $repoRoot 'artifacts/climate-learning'
    $dataRoot = Join-Path $runRoot 'data'
    $modeAlgorithm = if ($Aggressive) { 'worldgen-3.26.0' } else { 'worldgen-3.25.0' }
    if ($Aggressive) { Write-Host 'AGGRESSIVE MODE: wide ranges, no conservative penalty or early stopping. Start with 300 iterations.' }
    if (!$PrepareOnly -and $Iterations -eq 0) {
        $recommended = if ($Aggressive) { 300 } else { 100 }
        $choice = Read-Host "Additional candidate iterations [$recommended recommended]"
        if ([string]::IsNullOrWhiteSpace($choice)) { $Iterations = $recommended }
        elseif (![int]::TryParse($choice, [ref]$Iterations)) { throw 'Iterations must be an integer.' }
        $previous = @(Get-ChildItem -LiteralPath $runRoot -Directory -ErrorAction SilentlyContinue | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'checkpoint.json') } | Sort-Object Name -Descending)
        $previous = @($previous | Where-Object { (Get-Content -Raw -LiteralPath (Join-Path $_.FullName 'checkpoint.json') | ConvertFrom-Json).Algorithm -eq $modeAlgorithm })
        if ($previous.Count -gt 0 -and !$Resume) {
            Write-Host '0: Start a new run from the unchanged baseline'
            for ($i = 0; $i -lt [Math]::Min(5, $previous.Count); $i++) { Write-Host "$($i+1): Resume $($previous[$i].Name)" }
            $selection = Read-Host 'Choose run [0]'
            if ($selection -and $selection -ne '0') {
                $index = 0
                if (![int]::TryParse($selection, [ref]$index) -or $index -lt 1 -or $index -gt [Math]::Min(5, $previous.Count)) { throw 'Invalid run selection.' }
                $Resume = $previous[$index - 1].FullName
            }
        }
    }
    if (!$PrepareOnly -and ($Iterations -lt 1 -or $Iterations -gt 10000)) { throw 'Choose 1-10000 iterations.' }
    dotnet build src/WorldGen.ClimateTraining -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Trainer build failed.' }
    $observations = Join-Path $dataRoot 'observations.csv'
    if (!(Test-Path -LiteralPath $observations)) {
        Write-Host 'First-use setup: downloading WorldClim monthly normals and isolated Python packages. No training during preparation.'
        $environment = Join-Path $runRoot 'python'
        $python = Join-Path $environment 'Scripts/python.exe'
        if (!(Test-Path -LiteralPath $python)) {
            python -m venv $environment
            if ($LASTEXITCODE -ne 0) { throw 'Python 3.11+ is required for observation preparation.' }
        }
        & $python -m pip install 'numpy==2.2.6' 'rasterio==1.4.3'
        if ($LASTEXITCODE -ne 0) { throw 'Observation dependencies could not be installed.' }
        & $python (Join-Path $PSScriptRoot 'prepare-climate-observations.py') $dataRoot
        if ($LASTEXITCODE -ne 0) { throw 'Observation preparation failed. No training started.' }
    }
    if ($PrepareOnly) { Write-Host 'Observations prepared. Training was NOT run.'; return }
    $arguments = @('--data', $observations, '--iterations', "$Iterations", '--library', (Join-Path $repoRoot 'world'))
    if ($Aggressive) { $arguments += '--aggressive' }
    if ($Resume) { $arguments += @('--resume', $Resume) }
    else { $prefix = if ($Aggressive) { 'wild-' } else { 'run-' }; $arguments += @('--output', (Join-Path $runRoot ($prefix + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')))) }
    Write-Host 'Ctrl+C stops safely. The next launch can resume completed iterations.'
    dotnet run --project src/WorldGen.ClimateTraining -c Release --no-build -- @arguments
    if ($LASTEXITCODE -eq 2) { Write-Host 'Cancelled; checkpoint preserved.' }
    elseif ($LASTEXITCODE -ne 0) { throw 'Training failed; inspect the error above. Existing saves/defaults were not changed.' }
} finally { Pop-Location }
