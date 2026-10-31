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

LATEST: owner explicitly requested updating the actual launched game after the
previous candidate-only delivery. The normal root shortcut NOW selects
builds/slot-b/WorldGen.exe, native IL2CPP BuiltUtc2026-10-31T22:58:21.8587435Z,
GameAssembly SHA3d50a5c7b4398cc0935bf561cd5884e0093184c475faef122b33e0f80e0306e4.
The owner was informed of the existing cricket audio failure and then requested
actual activation; it remains a known issue, NOT a passing combat/audio suite.
Six appearance skins/textures exported under
assets-source/humans/appearance-runtime and rebound onto unchanged production
rigs in new Resources/HumanAppearance. Stable original character IDs/legacy
clip references retained; new library preferred with original library fallback.
No original art/rig/motion overwritten. Slot-b is selected and must not be
overwritten. At activation the owner still had slot-a process7748 open: leave
it untouched and do not rebuild slot-a until it exits. unity-check now protects
running slots as well as the selected slot. Editor images exist, but a fixture constructor
warning means its55-check run is diagnostic, NOT clean native release proof.
Native IL2CPP candidate built22:58:21Z in slot-b: appearance55, Kimodo86,
Legacy81 and shader39 checks PASS. Actual gameplay movement/item captures
inspected. Current Items156 checks PASS (prior deer cue failure did not recur).
Combat/audio STILL FAILS the native cricket decode check; do not claim it passed.
Both revised and original character libraries passed144 Editor carry-mask
checks: idle/walk/run arms stay unchanged, fingers grip, weapon actions apply.
Exact four carry runtime hashes match the native selected build. Their reviewed
carry-only hunks were adopted for the owner's explicit small-item fix request,
committed as24be330; no runtime source bytes needed changing or rebaking.
5048/5048 asset hashes verified; all prior4709 paths/hashes preserved. Five new
asset packs uploaded/download-verified in game-assets-human-appearance-20261031-v1.
Source checkpointdc290988 and lock1776c72 pushed. Current selection and known
limitations are in assets/verification/human-appearance-verification.json:
417 passing native checks including Items, plus144 Editor carry checks.

Owner deleted `WorldGen_OneShot_Prompt.md`; two untracked old deer scripts
`scripts/deer-review-controls.py` and `scripts/prepare-deer-review.py` must stay
untouched/unstaged. The previously prepared small-carry patches in
LocalTerrainFirstPersonArms, LocalTerrainNaturalHandling,
LocalTerrainSharedHumanActions and WorldGenSharedHumanMotion were reviewed and
committed as described above for the current request; preserve their behavior.
Original human art and animation sources remain preserved. The owner has now
authorized the revised appearance library, using unchanged production rigs.
Original approved human source SHA is
0946d797f3c620492444f2ec65d2a88280053fbda2c882dde3dfaf826942d229.

Do not close owner applications. No automatic resumption of old Homo/history,
animal-generation or overnight tasks. The old broad hunting wind/scent suite
has a known historical failure; do not change AI or hide it to force a pass.

## Completed layout migration; historical gameplay candidate

Owner explicitly authorized the complete filing redesign31Oct, including moving
required inputs out of artifacts. Both existing player aliases moved to builds;
selected root shortcut THEN `builds/slot-a/WorldGen.exe`, physical D package
unchanged, GameAssembly SHA5bf6b4bf748bd526193a82e93ffe740b6b42f6b06ddb0d91ae306f178c692fc6.
The same previously tested native human release remains selected. Source fixes
e004624/runner6dba20c (night lighting, audio attenuation/dawn, melee and bleeding)
have an IL2CPP candidate in slot-b, BuiltUtc2026-10-31T21:35:47.1776397Z.
39 native shader/skin-scale/audio-settings checks PASS. Broader Combat/audio
and Items checks FAILED: cricket decode timeout and deer cue-before-flight
assertion. That older candidate was not selected. Do not claim those suites passed. No AI
changes were made to force the old perception assertion green. Pending gameplay
validation is separate from the completed filing redesign.

29686 authoring/metadata files24.888GiB relocated with byte preservation; 26
supporting mesh/material files followed. 331 Git-tracked inputs moved out of
scratch. References rebased; metadata hash references refreshed; approved model,
texture, motion-array and audio bytes unchanged. Ten active tool directories
and report dependencies moved to tools. Details in artifacts/layout-migration
are disposable evidence; canonical paths and immutable hashes must be stored
outside scratch. Additional11 model/material/world reference files and3590
audio/source-media files preserved, SHA checked. An old shared sound junction
was exposed by the final verification; all1088 runtime clips restored to a real
sounds/fauna/ai directory with their original locked hashes. No clips lost.

Asset delta game-assets-layout-20261031-v1 published:4 new remote packs uploaded
and downloaded/hash-verified; existing immutable packs reused. Final current
lock4709/4709 SHA matches. Source checkpoint86233d9 and final lock/proof85bd5e6
PUSHED and remote main verified. No tracked artifacts; owner four runtime diffs
remain separate/uncommitted, so do not claim the whole workspace remotely clean.
OLD artifacts was renamed/unavailable for the full IL2CPP build, original deer
and human scene opens, exact fresh deer mesh/motion export (12skin checks), tool
CPU imports and final lock checks. Both dependency checks passed;13 offline
restore checks passed. All original scratch restored; no temporary root remains.
Permanent proof assets/verification/project-layout-verification.json records
scope and known gameplay failures. Source art/motion/audio bytes unchanged.
Local optional studies/auditions are retained, not all are cloud-lock inputs.

TripoSG's old scratch package fallback removed; its compatible torchvision
package copied/read-verified into the installed tools environment (old blocked
environment preserved). CPU preflight passes without scratch or GPU inference.
The build preflight now rejects lock/source/build/tool dependencies in scratch,
including shared physical output-junction targets and Python .pth fallbacks.
All migration-owned Unity/Blender/native jobs finished. Slot-a remained unchanged
at migration completion; current owner-requested selection is recorded above.

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
