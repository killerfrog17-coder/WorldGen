# WorldGen repository instructions

## Owner requirements: filesystem and builds

The entire `artifacts/` folder must be disposable without affecting gameplay,
source assets, authoring tools, builds, approved presets, reference contracts,
or personal saves. Never place unique or required input data there. Scratch
outputs may be recreated by their producing command. No compatibility junction
inside scratch may be needed for permanent input.

Canonical locations are defined in `assets/project-layout.json`:
- `assets-source/`: editable art, raw motions and necessary exporter inputs.
- `assets/`: manifests, recipes, provenance and verification contracts.
- `unity/WorldGen.Unity/Assets/`: Unity runtime/imported game content and code.
- `sounds/` and `music/`: current externally loaded runtime audio.
- `tools/`: authoring applications and local tool installations.
- `builds/slot-a` and `builds/slot-b`: ONLY two reusable player locations.
- `docs/reports/`: finished reports and their source references.
- `artifacts/`: logs, captures, previews, tests and derived temporary files only.

NEVER create per-feature/per-update full player folders. Invoke
`scripts/unity-check.ps1 -BuildPlayer` to reuse the inactive fixed slot; it must
reject other names and overwriting the selected slot. All player builds MUST
use IL2CPP, NEVER Mono, even for diagnostics. Do not make isolated full player
copies. Retire superseded binaries after checks/selection. Git and immutable
GitHub Release asset packs provide history; do not keep daily binary archives.
Run `scripts/audit-project-layout.py` before builds. Source/project settings use
Git; large required inputs use `assets/game-assets.lock.json` and free immutable
release packs. No paid services, LFS charges, signup or subagents. Installed AI
weights and user worlds are not cleanup targets. Preserve Unity `.meta` GUIDs.

## Current owner work to preserve

Owner deleted `WorldGen_OneShot_Prompt.md`; two untracked old deer scripts
`scripts/deer-review-controls.py` and `scripts/prepare-deer-review.py` must stay
untouched/unstaged. Other-thread prepared small-carry changes in these four
runtime files are not ours to revert or stage wholesale: LocalTerrainFirstPersonArms,
LocalTerrainNaturalHandling, LocalTerrainSharedHumanActions, WorldGenSharedHumanMotion.
They preserve gait arms for small carried items and are still uncommitted.
Unaccepted human physique/appearance work and its scripts are separate authoring
work, not an approved game model replacement. Preserve it without integrating
its geometry/materials. Original approved human source SHA is
0946d797f3c620492444f2ec65d2a88280053fbda2c882dde3dfaf826942d229.

Do not close owner applications. No automatic resumption of old Homo/history,
animal-generation or overnight tasks. The old broad hunting wind/scent suite
has a known historical failure; do not change AI or hide it to force a pass.

## Current migration / validation checkpoint

Owner explicitly authorized the complete filing redesign31Oct, including moving
required inputs out of artifacts. Both existing player aliases moved to builds;
selected root shortcut NOW `builds/slot-a/WorldGen.exe`, physical D package
unchanged, GameAssembly SHA5bf6b4bf748bd526193a82e93ffe740b6b42f6b06ddb0d91ae306f178c692fc6.
The same previously tested native human release remains selected. Source fixes
e004624/runner6dba20c (night lighting, audio attenuation/dawn, melee and bleeding)
are still PENDING fresh native proof; slot-b is the unselected candidate.

29686 authoring/metadata files24.888GiB relocated with byte preservation; 26
supporting mesh/material files followed. 331 Git-tracked inputs moved out of
scratch. References rebased; metadata hash references refreshed; approved model,
texture, motion-array and audio bytes unchanged. Ten active tool directories
and report dependencies moved to tools. Details in artifacts/layout-migration
are disposable evidence; canonical paths and immutable hashes must be stored
outside scratch. A new metadata-only/legacy-motion asset delta and final
artifact-free build/export/native tests remain before claiming migration done.

Old artifacts cleanup removed17 obsolete player packages and10267 disposable
captures/geometry dumps. Earlier45GB figure was across drives, not an assertion
that artifacts was empty or a guarantee of Steam's selected-drive free space.
Keep space measurements tied to actual OS readings; unrelated changes occurred.

## Blocked operations ? do not retry or work around

Automatic approval review previously rejected these operations before execution,
with only the reason "blocked by policy":
- Deleting C:/WorldGenBuildArtifacts/recovery-check.
- Deleting old E:/WorldGen-builds camp-water generated debug targets.
- Relocating/deleting D:/WorldGenBuildArtifacts/world-feedback-active-cpp to C.
- Deleting artifacts/tools/trellis2-venv or its pieces.

These targets remain untouched. The obsolete TRELLIS environment is not a
required project input and stays as disposable historical tool cache in scratch.
Do not delete it as part of a broad purge or move it as a deletion workaround.
A temporary artifact-directory rename for dependency testing must preserve and
restore all original contents; it is not permission to delete blocked data.
