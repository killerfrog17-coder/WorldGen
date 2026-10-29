param(
    [Parameter(Mandatory=$true)][string]$Plan,
    [Parameter(Mandatory=$true)][string]$Report
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
$audit=Get-Content -LiteralPath $Plan -Raw | ConvertFrom-Json
$index=Get-Content -LiteralPath (Join-Path $repo 'assets/legacy-builds.json') -Raw | ConvertFrom-Json
$head=(& git -C $repo rev-parse HEAD).Trim()
$remote=(& git -C $repo ls-remote origin refs/heads/main)
if($LASTEXITCODE -ne 0 -or !$remote -or ($remote -split '\s+')[0] -ne $head){throw 'The current checkpoint is not verified on remote main'}
& git -C $repo diff --quiet HEAD -- assets/game-assets.lock.json assets/legacy-runtime-assets.lock.json assets/legacy-builds.json
if($LASTEXITCODE -ne 0){throw 'Dependency locks or build inventory differ from the pushed checkpoint'}
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Do not purge build work while Unity is running'}
$roots=@((Join-Path $repo 'artifacts'),'E:\WorldGen-builds','C:\WorldGenBuildArtifacts') | ForEach-Object {[IO.Path]::GetFullPath($_).TrimEnd('\')}
$results=@()
foreach($entry in $audit.packages){
    if($entry.keep){continue}
    $target=[IO.Path]::GetFullPath($entry.path).TrimEnd('\')
    if(!($roots | Where-Object {$target.StartsWith($_+'\',[StringComparison]::OrdinalIgnoreCase)})){throw "Target outside owned build roots: $target"}
    $saved=$index.packages | Where-Object {$_.path -eq $entry.path -and $_.asset_snapshot -eq $entry.asset_snapshot}
    if(!$saved){throw "No backed-up asset snapshot for $target"}
    if(!(Test-Path -LiteralPath $target)){continue}
    $item=Get-Item -LiteralPath $target -Force
    if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Physical target must not be a directory link: $target"}
    if(!(Test-Path -LiteralPath (Join-Path $target 'WorldGen.exe')) -or !(Test-Path -LiteralPath (Join-Path $target 'WorldGen_Data'))){throw "Not an audited player package: $target"}
    $links=@(Get-ChildItem -LiteralPath $target -Recurse -Force | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint})
    if($links.Count){throw "Inspect nested directory links before deleting $target"}
    $selected=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $repo 'WorldGen.exe - Shortcut.lnk')).TargetPath
    $running=@(Get-CimInstance Win32_Process -Filter "Name='WorldGen.exe'" | ForEach-Object {$_.ExecutablePath})
    $protected=@($selected)+$running
    foreach($executable in $protected){
        if(!$executable){continue}
        $directory=[IO.Path]::GetFullPath((Split-Path -Parent $executable)).TrimEnd('\')
        if($directory -eq $target -or @($entry.aliases) -contains $directory){throw "Package became selected/running: $target"}
    }
    foreach($suffix in @('world','worlds','saves','Saves','SaveGames')){
        $candidate=Join-Path $target $suffix
        if((Test-Path -LiteralPath $candidate) -and (Get-ChildItem -LiteralPath $candidate -Force | Select-Object -First 1)){throw "Possible personal saves inside $target"}
    }
    $extraWorlds=@(Get-ChildItem -LiteralPath $target -Filter '*.world' -Recurse | Where-Object {$_.FullName -ne (Join-Path $target 'WorldGen_Data\StreamingAssets\world\Earth.world')})
    if($extraWorlds.Count){throw "Non-default world inside $target"}
    # All final absolute paths are checked above, before this recursive operation.
    Remove-Item -LiteralPath $target -Recurse -Force
    $results+=[ordered]@{path=$target;bytes=$entry.bytes;asset_snapshot=$entry.asset_snapshot}
    foreach($alias in $entry.aliases){
        $aliasPath=[IO.Path]::GetFullPath($alias).TrimEnd('\')
        if($aliasPath -eq $target -or !$aliasPath.StartsWith((Join-Path $repo 'artifacts')+'\',[StringComparison]::OrdinalIgnoreCase)){continue}
        $link=Get-Item -LiteralPath $aliasPath -Force -ErrorAction SilentlyContinue
        if($link -and ($link.Attributes -band [IO.FileAttributes]::ReparsePoint)){
            # DirectoryInfo.Delete removes only the now-dangling junction itself.
            $link.Delete()
        }
    }
    [ordered]@{removed=$target;GiB=[math]::Round($entry.bytes/1GB,3)} | ConvertTo-Json -Compress | Write-Output
    [ordered]@{checkpoint=$head;removed=$results;bytes=($results | Measure-Object bytes -Sum).Sum} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Report -Encoding UTF8
}
