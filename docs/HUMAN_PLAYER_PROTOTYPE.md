# Human player and movement prototype

The playable character uses one of the six unclothed MPFB adults from the existing human
audition, selected at random and retained in the play-session save. The original
MPFB skeleton, skin weights and already fitted animation bank are the inputs.
This does not use a newly generated or guessed rig. The existing animal assets
and animal renderer are separate.

## Controls

- Mouse wheel out: third person, up to 12 metres; wheel in: first person.
- Middle click: lock/unlock the requested camera distance, including first person.
- WASD: existing movement; C: walk; Shift: sprint; Space: jump; Ctrl: crouch pose
  and slower movement. A full crouch collision/capsule system is not implemented.
- F toggles flight in Creative only. Alt + wheel adjusts flight speed; plain
  wheel adjusts the camera. Flight deliberately uses a neutral body pose.
  Spectator always flies, hides the player and bypasses terrain/camera collision;
  its wheel/middle button do not alter the retained body-camera zoom settings.
- `/gamemode survival`, `/gamemode creative`, `/gamemode spectator` are the only
  mode selectors. `/gamemode` alone explicitly reports mode/vitals in chat.
  There are no new HUD elements or mode-selection menu controls.
- `/spawn human [count]`: queue nearby humans, one by default, without a fixed
  head-count cap. No NPC humans spawn automatically.
- `/spawn human clear`: remove command-spawned humans and cancel their queue.
  `/spawn clear` clears both manually spawned animals and humans.
- The `/humans` and `/runstyle` audition commands, their help and autocomplete
  entries are removed. Old optional run-style save values remain readable but
  are ignored; everyone uses the accepted jog and the separate sprint clip.

There is no control-instruction banner on the gameplay screen. Do not add
unsolicited control/tutorial overlays; commands/help remain available.

Third-person camera probes the existing terrain height field and pulls inward
before crossing the ground. It does not add terrain mesh colliders. The distance
lock locks user zoom, not obstacle avoidance. Tree/prop camera collision remains
future work. Turning and camera distance transitions are smoothed. Idle body
orientation is retained when the camera turns in either view; movement updates
the body's facing direction. Walking toward the camera then stopping keeps
the character facing it.

First person hides the entire local body in the owning camera's forward/depth
passes, keyed by a per-human ID. Shadow passes retain the complete person.
Another camera receives no hide ID, so the same person remains fully visible
there. NPCs and third-person bodies remain visible. There is no multiplayer
system yet. This supersedes the earlier first-person body/head-only mask.

## Game modes and survival needs

New and legacy sessions default to Creative, preserving the prior sandbox.
Creative has flight access, no survival drain/damage and an unlimited-resource
capability flag for future inventory/crafting. Selecting Creative restores
health, stamina, energy, hunger/thirst, temperature and wetness; it can recover a
dead prototype player. There is no inventory/resource system to bypass yet.
Spectator freezes needs and disables the entire player actor, including its
shadows, while retaining other humans. Its free camera passes through terrain,
including beneath the ground; signed altitude is saved and restored. On leaving
Spectator the normal body returns, and an underground position is lifted out of
terrain. Prop collisions otherwise remain at the previous implementation level.

Survival disables flight, including old saved flight flags and teleport-induced
flight. It retains normal third-person/first-person camera behavior. The saved
`PlayerSurvival` state contains Health, Stamina and Energy (100 is full), Hunger
and Thirst (0 is satisfied, 100 depleted), CoreTemperatureC and Wetness (0..1).
Short-term sprint stamina lasts about11s from full, recovers when not sprinting,
and uses exhaustion hysteresis. Longer-term energy limits maximum stamina and
recovers with rest when fed, hydrated and thermally comfortable. Jumping costs
stamina. Low energy or severe exposure slows movement; death blocks voluntary
movement and records its cause. No death overlay, automatic respawn, food/drink
interaction, clothing, fall/combat damage or drowning system is added here.

Needs use real elapsed gameplay time and the selected solar-day duration, not
historical calendar compression. Stamina uses real seconds. Exposure response
is calibrated around the default60-minute solar day and scales with its duration.
Pause, map, ecology/chat overlays, unfocused play and loading do not consume
needs; saves/offline time do not catch up metabolism. The local weather provides
temperature, humidity, rain and wind. A cached2Hz ecology probe supplies canopy
shelter/solar exposure and immersion, independently of graphics/shadow quality.
Exertion warms the player, raises thirst and costs energy. Heat primarily raises
water demand; excessive retained body heat can also damage health. Cold, wind,
wet skin and cold immersion lower body temperature. Insulation is an explicit
future input, currently zero; no clothing assets were added.

These are gameplay approximations with tuning constants, not a clinical model.
The [CDC heat/cold summary](https://www.cdc.gov/yellow-book/hcp/environmental-hazards-risks/heat-and-cold-illness-in-travelers.html)
supports the roles of exertion, evaporation/humidity, dehydration and wet/windy
cold exposure. The35C hypothermia threshold follows that source and the
[NHS definition](https://www.nhs.uk/conditions/hypothermia/); the game begins
progressive cold damage below it. Timing/damage rates are prototype balance.

67 focused survival/mode/save/movement assertions and22 previous camera/save
checks pass.15 actual Unity command/visibility checks cover mode switching,
noclip movement, body/NPC visibility, dead-player recovery and command removal.
Creative/Spectator/Survival renders were inspected. Evidence: `artifacts/game-modes`.

## Motion and scope

Unity's native skinned meshes and animation Playables blend idle, walk, jog,
sprint, crouch and jump. Locomotion clips share a normalized foot phase while
their blend weights ease between speeds. NPCs ease their speed and heading, make occasional
movement decisions, and avoid sampled water/steep terrain. These are demonstration
actors, not population/history simulation or functioning social AI. They are
not persistent NPC saves. A fitted swim clip is included in the bank; the existing
controller does not yet provide a swimming mechanic. Sitting, work, combat,
inventory, survival rules and a character creation screen are deferred.

The six fixed body/skin/hair examples are placeholders from the audition,
not a finished runtime genetics/creation system. Modern outfits are omitted;
their body-deletion masks are removed while the helper-geometry mask is retained,
so removing a shirt does not expose missing skin. No replacement clothing is
added yet. They retain source body proportions. Native bind geometry measures about
1.35–1.70 metres in height including hair, with 38,668–41,828 triangles per person
before later LOD work. Subdivision is omitted from runtime derivatives. Skin maps
use 2K; smaller materials use 512–1024 pixels, mipmaps and normal Unity texture
compression. Their large number of modelling shape keys is not imported.

MPFB's eye material uses a transparent outer corneal surface with black albedo.
The initial opaque runtime material incorrectly covered the iris and sclera with
that surface. Exported opacity now follows the native inner-eye UV coverage:
the outer layer is clipped, while whites, iris and opaque black pupil remain.
This preserves the underlying eye shape and pigmentation, with inexpensive
gloss on the retained inner eye. No expensive refractive eye shader is added.

Animal command counts32/64, total128 and species24 ceilings are removed.
Their pose buffer grows geometrically as slots fill, retaining live poses and
rebinding the GPU buffer. Explicit spawn batches remain lazy counts and are
prepared/admitted under per-frame time budgets. Natural placement no longer
truncates the total by ground/air/water/tiny category. Regions, density, habitat
and streaming still determine natural distribution; physical device buffer/
memory limits still apply. No active species is evicted to fit a model count.
Humans similarly queue large requests across frames with no16-person cap.
Cancellation commands clear queued requests as well as existing explicit spawns.

## Reproduction and sources

1. Preserve `artifacts/human-animation-preview/Human - Animation Comparison.blend`
   (SHA256 `0946d797f3c620492444f2ec65d2a88280053fbda2c882dde3dfaf826942d229`).
2. Run `scripts/export-human-runtime.py` in Blender background mode. It reads the
   audition without saving it, bakes unlit material colour/opacity on CPU, writes
   native FBX skins/actions and metadata to `artifacts/human-runtime/exports`.
   `--reuse-textures` retains existing accepted pigment/opacity maps. The three
   CMU BVHs listed below must be in `artifacts/human-running-sources/cmu`.
3. Stage the current Core/Presentation/Serialization DLLs, then run Unity batch
   `-executeMethod HumanRuntimeBuilder.Build`. It creates the six prefabs and
   `Resources/Humans/HumanLibrary.asset`. Generated data stays ignored by Git.
4. Build with the normal release player pipeline. The prefabs, native meshes,
   clips and maps are included through their Resources library dependencies.

MPFB tool code is GPL; the selected core/output and official system assets are
CC0, as described in the [MakeHuman/MPFB licence](https://www.makehumancommunity.org/about/license.html).
Non-running movement uses the free [Quaternius Universal Animation Library](https://quaternius.itch.io/universal-animation-library)
and [KayKit jump](https://kaylousberg.itch.io/kaykit-character-animations), both CC0.
Jogging and sprinting use [CMU captured motion](https://mocap.cs.cmu.edu/), free
for all uses under its stated permission, **not CC0**. Jogging retains accepted
16_35; sprinting uses the faster captured 143_01, with different body/limb motion.
Bruce Hahne/cgspeed's BVH conversion is linked by CMU's
[resources page](https://mocap.cs.cmu.edu/resources.php). Downloaded sources are
`https://raw.githubusercontent.com/una-dinosauria/cmu-mocap/master/data/016/16_35.bvh`
and `https://raw.githubusercontent.com/una-dinosauria/cmu-mocap/master/data/143/143_01.bvh`.
The local `sources.json` records exact hashes; runtime acknowledgements ship in
`StreamingAssets/HUMAN-SOURCES.txt`. Original 53-bone skins/rests remain the
inputs; source T-poses calibrate rotations and complete strides loop with a
shared foot phase. Runtime cadence uses measured travel distance. These are
retargeted captures, without contact IK or a guarantee against all foot sliding.

Exact earlier downloaded files and provenance remain in
`artifacts/human-animation-sources`. No tool code, paid service or neural inference
is required in the game. See the earlier HUMAN_CHARACTER_BUILDER_DEMO and
HUMAN_ANIMATION_PREVIEW documents for the original foundation and retargeting.

## Checks

`dotnet run --project tests/WorldGen.Core.Tests -c Release -- --player-camera-only`
checks camera lock/zoom limits, terrain obstruction, old-save defaults and new
identity/camera save round trips. Opt-in release-player `human-review` in the
existing performance harness checks real skins/clips, spawning, first-person
body and separate-camera head visibility. These are prototype checks, not an
all-terrain animation/contact-IK or multiplayer certification.

Initial release player `player-human-prototype` built
2026-10-27T03:31:19.5268805Z. Final `human-review` passed935checks, including all
six skins/seven clips at three phases, NPC commands/movement, body/head masking
and a second actual camera. Root inspected rendered front/side views of all six
people and final first-person/body/head/other-camera images. Terrain input SHA
remained unchanged; no player exceptions/errors in that run. This initial build
and the previous `player-daylight-fill` remain available for comparison.

The earlier refinement is `player-human-refinement`, built
2026-10-27T04:12:37.0559529Z. It removed the control
banner and modern outfits, fixes cornea opacity, preserves idle facing, adds the
three KayKit run alternatives and removes fixed population/type caps. Runtime
review passes1162checks, including24live humans,160explicitly spawned animals,
GPU palette growth/readback and stationary facing. All six naked body variants,
six face/eye views and five run/jog/sprint samples were actually inspected, along
with first-person body and head/other-camera checks.20focused camera/save/large-
request parser checks pass. No claim that every alternative run is better;
the style selector lets the owner compare. No multiplayer or unlimited-hardware
claim is implied by removing population caps. Evidence is under ignored
`artifacts/human-refinement`.

## Pre-night rollback and upright running revision - 27 October

The owner scrapped the overnight survival and early-Homo work. Source was
restored to pre-night `7ba44ebdabdd7b0dfce3f3887b55f64e5b305e2a`; the discarded
night is retained on GitHub branch `archive/scrapped-overnight-20261027`
(`7cd31308e696fa7046fdf5b42a4daf4cdeeeecae`) and in a local source archive.
Survival pickups, structures, inventory and the incomplete history changes are
removed. Saved worlds and sessions were not modified. Sessions saved by the
discarded version are not promised compatible with the restored version; keep
them for the archived build rather than silently converting them.

The earlier IL2CPP `player-upright-running`, built
2026-10-27T17:45:30.8700811Z used the then-eight-clip bank and body
visibility described above. It preserves accepted skin/eye PNGs byte-for-byte
(42 maps), original mesh geometry/UVs/weights and the source native file. FBX
bind-matrix rounding is below 1e-6. In the A-skin motion audition, mean torso
lean was about 10-13 degrees for the chosen captures, versus 29 for the old
jog and 41 for the old sprint; these measurements are not universal anatomical
quality scores. Source files remain local and unchanged.

The focused camera/save suite passes 20 assertions. Actual Unity renders cover
all six people and three new runs at two phases. A 335-check review verifies
skin channels, changing evaluated poses, measured stride metadata, whole local
body hiding, retained full shadows and another camera seeing the same person.
Same-frame editor captures need `forceMatrixRecalculationPerRender`; initial
cached-pose captures were rejected. Evidence: `artifacts/human-running-runtime`
and the Blender motion audition under `artifacts/human-running-review`. This
does not establish perfect foot contacts or owner acceptance of the new runs.

## Manual human spawning and dedicated sprint - 27 October

Release IL2CPP `player-human-sprint` built 2026-10-27T18:10:01.8902259Z,
selected by the existing shortcut. Prior `player-upright-running` is retained.

The subsequent owner revision removes automatic demo humans and both audition
commands. `/spawn human [count]` is the entry point, one by default; specific
human clear and shared `/spawn clear` cancel queued requests as well as live
NPCs. No new population limit is introduced. Old optional run-style save data
is readable but no longer controls animation.

The selected movement bank is now seven roles: Idle, Walk, Run (jog), Sprint,
Crouch, Jump and Swim. The accepted jog retains CMU16_35. Sprint replaces the
slower35_17 take with CMU143_01, measured at approximately 6 m/s from its root
travel. It supplies different limb/body motion rather than accelerating the
jog. Shared gait phase, smooth weight changes and stride-based cadence remain.
The faster source has a short complete stride, so the offline stride selector
now admits periods down to 0.35 s. Checked raw endpoints before accepting that
cycle; the retained jog still selects the same frames 51..146.

Evidence is under `artifacts/human-sprint-runtime`: source candidate metadata,
stride diagnostics, CPU Blender auditions and actual Unity renders of all six
sprints. 22 camera/save/help assertions and 334 Unity skin/render/command checks
pass. The opt-in editor review reloads Resources after NewScene, which can
unload stack-only asset references. No contact-IK or perfect all-terrain foot-placement claim.

The broader `--play-session-only` check fails at its existing physical-noise
macro-anchor assertion. An isolated archive of the pre-human HEAD reproduced
the same failure at PlaySessionTests.cs:32; this is not a new human/save regression
and was not changed here. New camera/identity round-trip tests pass. Evidence
is ignored under `artifacts/human-runtime`, with rejected first mask/collar
captures retained separately from `final-review`. No FPS improvement is claimed.
