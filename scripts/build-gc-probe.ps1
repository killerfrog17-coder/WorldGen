param([string]$Output='artifacts/gc-probe')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$destination=[IO.Path]::GetFullPath((Join-Path $repo $Output))
$scratch=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts'))+[IO.Path]::DirectorySeparatorChar
if(!$destination.StartsWith($scratch,[StringComparison]::OrdinalIgnoreCase)){throw 'Diagnostic binaries belong inside artifacts'}
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$installation=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$installation){throw 'Installed x64 C compiler not found'}
$vcvars=Join-Path $installation 'VC/Auxiliary/Build/vcvars64.bat'
$source=Join-Path $repo 'tools/worldgen-gc-probe/probe.c'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$dll=Join-Path $destination 'WorldGenGcProbe.dll'
$batch=Join-Path $destination 'compile.cmd'
# Paths are fixed local arguments, not user text or command substitutions.
foreach($path in @($vcvars,$source,$destination)){if($path -match '["%\r\n]'){throw 'Unsupported compiler path'}}
@"
@echo off
call "$vcvars" >nul
if errorlevel 1 exit /b 1
cl /nologo /O2 /MT /LD /W4 /WX "$source" /Fo"$destination\probe.obj" /Fe"$dll" /link /INCREMENTAL:NO
"@ | Set-Content -LiteralPath $batch -Encoding ascii
Push-Location $destination
try { & $env:ComSpec /d /c $batch; if($LASTEXITCODE -ne 0){throw 'GC probe compilation failed'} }
finally { Pop-Location }
Get-FileHash -LiteralPath $dll -Algorithm SHA256
