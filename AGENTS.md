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
reject other names and overwriting the selected slot. OWNER1Nov BUILD POLICY
SUPERSEDES all older IL2CPP-only/never-Mono notes: use fast compiler/Editor
iteration and development players as appropriate. Only make IL2CPP players for
major playable updates when the owner explicitly requests one; the first planned
milestone is Alpha1.0.0 when the Paleolithic feature set is first complete. Do not
repeat native packaging after each small edit. The build script/editor builder
now default to managed development players. Pass -Il2Cpp explicitly for an
owner-requested major native release.
Do not make isolated full player
copies. Retire superseded binaries after checks/selection. Git and immutable
GitHub Release asset packs provide history; do not keep daily binary archives.
Run `scripts/audit-project-layout.py` before builds. Source/project settings use
Git; large required inputs use `assets/game-assets.lock.json` and free immutable
release packs. No paid services, LFS charges, signup or subagents. Installed AI
weights and user worlds are not cleanup targets. Preserve Unity `.meta` GUIDs.

## Current owner work to preserve

SELECTED2Nov OWNER SHELTER REVISION: root shortcut builds/slot-b/WorldGen.exe,
Mono DEVELOPMENT BuiltUtc2026-11-02T05:18:51.2766531Z, production07075cd pushed.
12 minimal menu entries: material store/bone lodge/stone hut hidden, legacy IDs
retained. B opens; windbreak scroll adjusts height. No covering menu; supplied
material determines cover, creative copies held material without consuming it.
Crossed trestles/one hanging rail; cone band gaps fixed; new version2 A-frame
front folds share side panels, old v1 save layout retained. Sized sound bones
substitute for rigid posts/beams; bent ribs wood. Ornamental binding coils
removed, material requirements retained. Ordinary stone icons input alpha fixed.
Crouch upright torso/relaxed arms, anatomical crouched camera. Core13102 PASS;
preceding candidate 261 shelter/icon/crouch checks PASS/exit0; final player
focused upload PASS. Final delta only restores legacy v1 rack geometry; new
plan and human/runtime code unchanged. Do NOT claim261 on final package.
971 source inputs/3254 original assets/30 legacy/6x70 bank/fauna/audio exact.
Evidence artifacts/shelters-20261102/revision-check +compatible-proof. Manifest
assets/verification/paleolithic-shelters-20261102.json. Owner visual review pending;
no original animation assets changed. No NPC work, paid/subagents/extra players.
Selected package FROZEN. Preserve owner3 files; no dump cleanup retries.

HISTORICAL PRIOR REVIEW:
SELECTED2Nov PALEOLITHIC SHELTER REVIEW: existing root shortcut NOW
builds/slot-a/WorldGen.exe, Mono DEVELOPMENT BuiltUtc2026-11-02T04:25:23.4377564Z,
productionfa67c7d pushed. Owner's newer fast-iteration policy applies. Shared
15-design catalog /4covers, bounded dimensions/costs, actual donor-coat roof
patches and fitted working hides, stone/bone footings, elevated beds/cache,
storage/drying and original five save keys retained. Pebble/small/large naming
and separate wide glyphs; taller chipping glyph ONLY actual eligible stones.
Instant drink no longer stages/drops held items: no-bag spear+stone blade tested
through real freshwater/sea input and animation, exact inventory/hand identity.
Final SAME PACKAGE 257shelter/39water checks PASS/exit0/clean errors;
quick upload/main-thread cancellation and10terrain boundary checks PASS. All15
plan captures and mixed donor roof reviewed; final working-frame screenshot
reviewed after correcting normalized hide scale. Core13429blueprint/45stone/
82inventory/69camp/69save/24physiology PASS. Proof971frozen/3254original/30legacy/
6x70bank/fauna+originalaudio EXACT. Manifest assets/verification/paleolithic-
shelters-20261102.json. Source/selected package FROZEN; no owner visual approval
inferred. Surface-built architecture only: caves terrain; excavation/storage pits
and snow refuges deferred pending terrain/snow editing. NPC work awaits approval.
Mono thread guards exposed original MeshData allocate/release off-thread use:
main-thread preallocation + SynchronizationContext deferred release corrected.
Earlier failed checks1..8 excluded; later final results only above. No whole-world
FPS claim or all historical hunting suites passing. Two temporary terrain dump
deletions rejected by automatic approval review; left intact, do NOT retry.
Owner slot-b process5296 untouched; while running it protects old build slot.
No owned build/test jobs remain. Preserve owner prompt deletion and2untracked
deer scripts; no subagents/paid/extra full player folders. Await feedback.

CURRENT1Nov EXIT / THROW / NEEDS / FIRE SELECTED: normal root shortcut NOW
builds/slot-b/WorldGen.exe, IL2CPP Release BuiltUtc2026-11-02T01:21:11.3166520Z,
production1abf493 pushed. AltF4 explicit discard exits self immediately, no save
or Unity/native resource teardown; focused input only. Final native111ms/exit0,
single Alt/F4 do not exit, existing save sentinel unchanged. Own terrain worker
already has parent-death watchdog. Combat aim/collision-floor reads resident
height before analytic fallback, saved ground-offset datum retained. Original
item sounds prepared asynchronously during loading with explicit sample decode;
24 importer background flags changed, original WAV bytes/GUIDs unchanged.
Loading-time inactive disposable collider/body initialization removes first-use
cost from first release; creates no saved inventory/world item. Final instrumented
first wooden throw21.477ms, next stone/bone1.813/1.580ms, actual original swish
plays.50ms gate kept. Stamina ready/hold/release much lighter and mass-dependent;
normal full spear throw~1point,10s aimed throw<3.5. Bow/melee unchanged.
HUD food half after1comfortable solar day, thirst half at1L deficit(~10h baseline);
saved FoodNeedDays, proportional meal relief, old-save migration, low fill reserved
for severe deprivation. Intermittent need pulses; existing lethal needs unchanged.
Fires roughly2x fine-fuel/3x wood tending time, longer coals. Feeding dry tinder or
kindling to live coals automatically revives fire; shared placement/relight predicate
includes grass/leaves/reeds/bark/raw+prepared fibre/tinder. Wet/cold ash blocked.
136 SAME FINAL PLAYER native PASS/exit0/clean runtime errors:131 throw/HUD/fire,
5exit. Throw test normal hidden windowed D3D11(real audio), exit batch. HUD half/
critical images inspected. Native fuel fixture burns8h then grass->kindling->wood
and kindling-only coals revive via actual survival material transfer.581 scoped
Core PASS:82item/337HUD/24physiology/69save/69camp.957frozen/3254protected/30Legacy/
6x70banks exact; original fauna/audio data exact. Layout5655/5048locked PASS.
Manifest assets/verification/throw-exit-20261101.json; evidence artifacts/throw-exit-
20261101/native-throw-warm,native-quit-warm,package-proof-warm. Earlier fixture
factory mistake, readiness failure and824ms/89ms intermediate timings excluded
from final hitch acceptance. Old broad items suite failed uninjured deer cue at
0.6s: AI unchanged, NOT claimed passing. Prior cricket decode/Earth golden remain
outside this fix. No universal FPS/perception/audio listening quality claim.
Owner criticized90+min turnaround: avoidable test mistakes and repeated native
packaging cycles. Future fixes: use cheaper compiler/Editor checks, group edits,
package once when reviewable; keep IL2CPP ONLY. No further polish/testing this
task after final checks. Two slots only, artifacts disposable, no cleanup/player
copies. Owner apps and prompt deletion/2deer scripts untouched. All owned jobs
finished. Await feedback; no old overnight work resumption.

HISTORICAL1Nov CAMP BLUEPRINTS SELECTED: normal root shortcut THEN builds/slot-a/
WorldGen.exe, IL2CPP Release BuiltUtc2026-11-01T23:29:49.5781554Z, production
6c457f9 pushed. V opens five plans: lean-to, windbreak, drying rack, material
store, brush hut. First corner/end then size+confirm; hut centre/radius; R15deg,
ShiftR reverse, RMB back/cancel. E supplies actual suitable material and timed
component labour; ShiftE details. Partial work, materials, terrain footings and
stock save. Creative supplies components instantly. Finished weapons excluded
from construction wood; shortening needs cutting edge and returns real offcuts.
Shelter weather protection, solid two-sided collision, physical low-roof crouch,
usable hut doorway; dimensional stores feed Nearby crafting without carry mass.
Weather-driven rack drying/smoking preserves calories/source animal identity.
Dynamic fire/bedding retained, extra covering/layers and recoverable stone ring.
Normal free-form frame placement entry replaced; existing saved frames retained.
Longer branch resources (mostly short, max4.5m) preserve positions/counts/IDs/tree
proximity. First-pass procedural structure art, no new construction animations,
NPC camps, general spoilage or wildfire. Offscreen weather catch-up approximate.
160 SAME FINAL PLAYER native checks PASS/exit0/clean runtime error logs:62 plan
and98 camp. Actual mouse/keyboard sizing/rotation, survival labour/occupied hands,
partial/full save, hut entry, crouch/headroom, protection, stock/drying/dismantling,
fire/cooking/sleep/hammering. All5 completed structure renders inspected; diagnostic
avatar head hidden by retained FPS mask, not an art edit. Focused Core1788 plan/
3658 branch/33forage/39camp/82inventory/69save PASS. Branch fixture95.1% tree-derived.
Proof955frozen/3254protected/30Legacy/6x70banks exact; original fauna/audio exact.
Layout5655PASS/5048locked inputs. Manifest assets/verification/camp-blueprints-
20261101.json; docs/CAMP_SYSTEM.md. Final evidence artifacts/camp-blueprints-
20261101/native-final-verified,native-camp-verified,package-proof-verified.
Earlier flat-ground fixture/obstructing test rack/legacy feet-based aim failures
excluded. Last rebuild changes only calibrated-camera QA aiming, assertions kept.
Old cricket decode and standalone Earth golden discrepancy remain NOT passing;
no universal visual/FPS/audio regression claim. Two fixed build slots reused,
no player copies or cleanup, all artifacts disposable. Owner apps untouched;
owner prompt deletion and2 deer scripts preserved. All owned jobs finished.
No remaining task development; await feedback, no old overnight task resume.

HISTORICAL1Nov FRAME / PLAYER COMFORT SELECTED: normal root shortcut THEN
builds/slot-b/WorldGen.exe, IL2CPP Release BuiltUtc2026-11-01T21:29:46.1664161Z,
production337cc40 pushed. Wooden frames use two real contacts: driven forked
posts / post tops, ground feet, resting beams and timed lashed side joints.
Cordage/sinew/rawhide consumed by actual mass and saved in recoverable joints;
support/load graph evaluated after edits, unsupported wood falls using existing
item physics. No coverings, construction animations or engineering simulation.
Right-click wood > Position as frame wood; click two contacts; wheel rolls;
E frame menu lashes/unties. Raw fork branches retain Y; trimmed poles do not.
Round fork/twig geometry fixed for anisotropic wood scaling. Geographic frame
axes survive chart shifts and convert correctly when a piece falls.
Breathing now quiet at fresh exertion and smoothly rises with reserve depletion;
gain/filter smoothing replaces early heavy threshold, stamina itself unchanged.
Player idle/idle carry hold steady; FPS eye anchored to stable body origin rather
than animated gait/head, crouch/jump/terrain motion retained. NPC idle unchanged.
Separate single white flakeable-stone PNG (ordinary Git asset) replaces composite
marker; generated image/prompt in assets/item-art/flakeable-stone-pickup-v1.json.
56 SAME FINAL PLAYER native PASS/exit0/clean error logs:41 frame+comfort,15 icons.
293 focused Core checks PASS:25frame/41effort/39camp/37wood/69save/82inventory.
Frame/lashing and native GPU34px icon images inspected. Head is hidden in the
diagnostic frame capture because it retains the FPS mask. Final icon fixture
uses shared runtime texture/UV selection with offscreen GPU drawing: earlier
hidden-window GUI repaint attempts FAILED and are excluded. Intermediate builds
and proofs are diagnostic, final evidence artifacts/frame-building-20261101/
native-release,native-icons-release,package-proof-release. Proof952frozen/
3254protected/30Legacy/all6x70banks exact; original audio/fauna exact. Layout
audit5655PASS including5048 locked inputs. Manifest assets/verification/frame-
building-20261101.json. Earlier standalone Validate stage-hash discrepancy and
known cricket audio decode failure remain NOT passing/unresolved; no full
audio/world-hash regression claim. Owner apps untouched; all own jobs finished.
Only two permanent build slots; no full player copies or cleanup this task.
Owner prompt deletion/deer scripts preserved. Await feedback; no old task resume.

HISTORICAL1Nov ITEM TOUCHUPS SELECTED: normal root shortcut THEN builds/slot-a/
WorldGen.exe, IL2CPP Release BuiltUtc2026-11-01T06:41:04.5110214Z, source9f309e9
pushed. Compact3kg stones use one hand; two-hand support from8kg or middle
span>.24m / longest>.55m, independent of saved small/large label. Flaking marker
shows core + detached flake using the original white category atlas and actual
WorkpieceReason. Bee/fly buzz requires an active enabled actually drawn/faded-in
model; range1.5m/.9m, quieter inverse-distance level. No new source art/audio.
1050 scoped Core checks PASS (82inventory/38stone/69sound/77use/784craft), native
11PASS/exit0/clean error log. Actual icon screenshot inspected after GUI repaint;
earlier10-check image was captured early and is NOT visual proof. Package proof
950 frozen/3254 protected/30 Legacy files/all6x70 banks exact. Prior cricket
native decode failure remains known; do not claim full audio regression passed.
Manifest assets/verification/item-touchups-20261101.json. Owner slot-b process
was left running; do not overwrite any selected OR running slot. Only two fixed
slots, no player copies. All owned Unity/native jobs finished. Final manifest/
shortcut note needs closing push; owner prompt deletion remains untouched.


HISTORICAL31Oct: owner explicitly requested updating the actual launched game after the
previous candidate-only delivery. The normal root shortcut NOW selects
builds/slot-b/WorldGen.exe, native IL2CPP BuiltUtc2026-10-31T22:58:21.8587435Z,
GameAssembly SHA3d50a5c7b4398cc0935bf561cd5884e0093184c475faef122b33e0f80e0306e4.
The owner was informed of the existing cricket audio failure and then requested
actual activation; it remains a known issue, NOT a passing combat/audio suite.
Six appearance skins/textures exported under
assets-source/humans/appearance-runtime and rebound onto unchanged production
rigs in new Resources/HumanAppearance. Stable original character IDs/legacy
clip references retained; new library preferred with original library fallback.
No original art/rig/motion overwritten. Slot-b was selected for that release. At activation the owner still had slot-a process7748 open: leave
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
