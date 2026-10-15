$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$source=Join-Path $repoRoot 'music'
$destination=[IO.Path]::GetFullPath((Join-Path $repoRoot 'src/WorldGen.Web/wwwroot/music'))
if($destination -ne [IO.Path]::GetFullPath((Join-Path $repoRoot 'src/WorldGen.Web/wwwroot/music'))){throw 'Unexpected generated music path'}
# This is only a disposable browser copy. Never remove or import the user's source tracks.
if(Test-Path -LiteralPath $destination){Remove-Item -LiteralPath $destination -Recurse -Force}
New-Item -ItemType Directory -Force $destination | Out-Null
$tracks=@(Get-ChildItem -LiteralPath $source -File | Where-Object {$_.Extension -in @('.mp3','.ogg','.wav')} | Sort-Object Name)
foreach($track in $tracks){Copy-Item -LiteralPath $track.FullName -Destination $destination}
$catalog=ConvertTo-Json -InputObject @($tracks | ForEach-Object {$_.Name}) -Compress
[IO.File]::WriteAllText((Join-Path $destination 'catalog.json'),$catalog,[Text.UTF8Encoding]::new($false))
