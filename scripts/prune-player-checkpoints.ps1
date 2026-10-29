param([switch]$Apply,[switch]$CopyRetainedToC,[switch]$KeepLatestThree)
$ErrorActionPreference='Stop'
throw 'Daily/latest-three build retention is retired. Use scripts/plan-build-cleanup.py and scripts/remove-obsolete-builds.ps1 after verified asset publication. See docs/ASSET_VERSIONING.md. No files were changed.'
