# Human player and movement prototype

The playable character uses one of the six MPFB adults from the existing human
audition, selected at random and retained in the play-session save. The original
MPFB skeleton, skin weights and already fitted animation bank are the inputs.
This does not use a newly generated or guessed rig. The existing animal assets
and animal renderer are separate.

## Controls

- Mouse wheel out: third person, up to 12 metres; wheel in: first person.
- Middle click: lock/unlock the requested camera distance, including first person.
- WASD: existing movement; C: walk; Shift: sprint; Space: jump; Ctrl: crouch pose
  and slower movement. A full crouch collision/capsule system is not implemented.
- F retains existing flight. Alt + wheel adjusts flight speed; plain wheel now
  adjusts the camera. Flight deliberately uses a neutral body pose.
- `/spawn human 3` or `/humans 3`: add nearby demo humans (maximum 16).
- `/humans clear`: remove the demo humans. Three try to spawn automatically
  on nearby dry, gentle ground once the initial terrain is available.

Third-person camera probes the existing terrain height field and pulls inward
before crossing the ground. It does not add terrain mesh colliders. The distance
lock locks user zoom, not obstacle avoidance. Tree/prop camera collision remains
future work. Turning and camera distance transitions are smoothed.

First person retains torso, arms and legs. Head/hair/eyes and the upper-neck mask
are hidden only in the owning camera's forward/depth passes, keyed by a per-human
ID. Shadow passes retain the complete head. Another camera receives no hide ID,
so this does not mutate a shared mesh, disable head objects, or make a networked
player intrinsically headless. There is no multiplayer system yet.

## Motion and scope

Unity's native skinned meshes and animation Playables blend idle, walk, jog,
sprint, crouch and jump. Locomotion clips share a normalized foot phase while
their blend weights ease between speeds. NPCs ease their speed and heading, make occasional
movement decisions, and avoid sampled water/steep terrain. These are demonstration
actors, not population/history simulation or functioning social AI. They are
not persistent NPC saves. A fitted swim clip is included in the bank; the existing
controller does not yet provide a swimming mechanic. Sitting, work, combat,
inventory, survival rules and a character creation screen are deferred.

The six fixed body/skin/hair/clothing examples are placeholders from the audition,
not the eventual prehistoric wardrobe or a finished runtime genetics/creation
system. They retain source body proportions. Native bind geometry measures about
1.35–1.70 metres in height including hair, with 38,240–49,764 triangles per person
before later LOD work. Subdivision is omitted from runtime derivatives. Skin maps
use 2K; smaller materials use 512–1024 pixels, mipmaps and normal Unity texture
compression. Their large number of modelling shape keys is not imported.

## Reproduction and sources

1. Preserve `artifacts/human-animation-preview/Human - Animation Comparison.blend`
   (SHA256 `0946d797f3c620492444f2ec65d2a88280053fbda2c882dde3dfaf826942d229`).
2. Run `scripts/export-human-runtime.py` in Blender background mode. It reads the
   audition without saving it, bakes unlit material colour/opacity on CPU, writes
   native FBX skins/actions and metadata to `artifacts/human-runtime/exports`.
3. Stage the current Core/Presentation/Serialization DLLs, then run Unity batch
   `-executeMethod HumanRuntimeBuilder.Build`. It creates the six prefabs and
   `Resources/Humans/HumanLibrary.asset`. Generated data stays ignored by Git.
4. Build with the normal release player pipeline. The prefabs, native meshes,
   clips and maps are included through their Resources library dependencies.

MPFB tool code is GPL; the selected core/output and official system assets are
CC0, as described in the [MakeHuman/MPFB licence](https://www.makehumancommunity.org/about/license.html).
Movement uses the free [Quaternius Universal Animation Library](https://quaternius.itch.io/universal-animation-library)
and [KayKit character animations](https://kaylousberg.itch.io/kaykit-character-animations),
both CC0. Exact downloaded files and provenance remain in
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

Final release player `player-human-prototype` built
2026-10-27T03:31:19.5268805Z. Final `human-review` passed935checks, including all
six skins/seven clips at three phases, NPC commands/movement, body/head masking
and a second actual camera. Root inspected rendered front/side views of all six
people and final first-person/body/head/other-camera images. Terrain input SHA
remained unchanged; no player exceptions/errors in the final run. The existing
shortcut selects this build; previous `player-daylight-fill` remains available.

The broader `--play-session-only` check fails at its existing physical-noise
macro-anchor assertion. An isolated archive of the pre-human HEAD reproduced
the same failure at PlaySessionTests.cs:32; this is not a new human/save regression
and was not changed here. New camera/identity round-trip tests pass. Evidence
is ignored under `artifacts/human-runtime`, with rejected first mask/collar
captures retained separately from `final-review`. No FPS improvement is claimed.
