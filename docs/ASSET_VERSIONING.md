# Restoring a WorldGen checkpoint

Source, project settings, recipes and ordinary assets use Git. Large source art
and runtime media use free GitHub Release packs, referenced by the **checked-out
version** of `assets/game-assets.lock.json`. No Git LFS subscription is required.
Release packs are immutable dependencies; do not delete a release referenced by
Git history. Executables, Unity caches, AI model weights and personal world saves
are not part of these packs.

After cloning or checking out a complete checkpoint:

The tested combined camp/water checkpoint is tagged
`checkpoint/camp-water-20261029`. Data-release tags hold asset packs; use the
code checkpoint tag when restoring a playable source version.

```powershell
python -m pip install -r scripts/requirements-assets.txt
python scripts/restore-game-assets.py
python scripts/restore-game-assets.py --verify-only
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/unity-check.ps1 -BuildPlayer
```

The restore tool uses Python 3.11 or later. The build additionally needs the Unity editor version in `ProjectVersion.txt`,
its Windows build support, a free activated Unity Personal license and the .NET
SDK. These tools are installed separately, not uploaded with the game. Use
`-Il2Cpp` for a release build with Unity's IL2CPP/Windows C++ prerequisites.

Restoration downloads one pack at a time, checks both pack and individual file
SHA256 hashes, then removes the temporary download. Unchanged local files are
reused. Modified local assets are refused unless `--replace` is explicitly given.
`--subset runtime` skips original authoring files under `artifacts`; use the
default complete restore when rebuilding animal/human exports. Public release
downloads work without a GitHub account. Publishing uses the existing local Git
credential helper without writing or printing its token. Native system TLS trust
is supported through `truststore`; certificate verification stays enabled.

The initial complete asset checkpoint preserves the original 177 animal Blender
files, the approved human Blender file and source maps, export inputs, runtime
bundles, audio, music and default/static world data. The 178 source Blender files
were also opened read-only to check for missing external images/libraries; their
dependencies are packed. The user's generated worlds remain separate local data.

## Older versions

Earlier Git commits did **not** track all these ignored assets. Their source can
be checked out, but a byte-identical rebuild is not guaranteed. Before deleting
old test players, unique packaged asset sets were inventoried in
`assets/legacy-builds.json`; their payloads are preserved by
`assets/legacy-runtime-assets.lock.json`. To inspect those files separately:

```powershell
python scripts/restore-game-assets.py --lock assets/legacy-runtime-assets.lock.json --destination artifacts/recovered-old-assets
```

This recovers historical asset data, not old executable binaries or a guarantee
that every old Unity import can be reproduced exactly. New complete checkpoints
include a dependency lock and can be rebuilt with their matching source.

## Creating the next checkpoint

```powershell
python scripts/game_asset_inventory.py artifacts/asset-publication/inventory.json
python scripts/publish-game-assets.py --inventory artifacts/asset-publication/inventory.json --tag game-assets-YYYYMMDD-vN --work artifacts/asset-publication/vN --publish
python scripts/restore-game-assets.py --verify-only
```

Commit and push the source change before publication, so the asset release can
anchor to an existing remote source commit. Publication deduplicates against the
previous lock. Only changed content uploads;
every new remote pack is downloaded and hash-verified before publication completes.
Commit and push the new lock alongside its source change, then verify the remote
commit. Never remove a locally unique input before that verification.

Keep the selected playable package and current build work. The build script
automatically reuses the inactive one of two fixed slots (their existing names
are `player-human-natural-review` and `player-world-feedback`). It refuses new
per-feature folder names and refuses to overwrite the selected playable slot.
After checking and selecting an update, remove the superseded compiled player;
its slot becomes the workspace for the next update. Never retain old players
as version history. Git source and immutable dependency packs serve that purpose.
Old compiled players are disposable after the dependency audit and remote checkpoint are complete.
Do not retain one build per day, duplicate source ZIPs, or caches of historical
packs. `plan-build-cleanup.py` audits only explicit build roots; the guarded
PowerShell cleanup refuses selected/running packages and saves. Original source
art and the owner's world library are not cleanup targets.

GitHub documents release assets as unmetered storage/downloads, with a limit of
2 GiB per individual asset. Packs are much smaller and contain this repository's
game dependencies: [GitHub release limits](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases#storage-and-bandwidth-quotas).
