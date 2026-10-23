# Deer behaviour asset and Blender viewer

23 October 2026. Offline prototype only; no Unity, ecology, spawning, saved-world,
game settings or executable changes. The owner resumed work after the gaming
pause. No further GPU inference, paid services or model/texture generation.

## Open and inspect

Desktop **Deer - Behaviour Prototype** opens the finished review, starts its
routine, and shows controls in the **Deer** sidebar. **Pause / Play** is also in
the viewport header. The old Game Asset Review shortcut remains available.

The dropdown provides five modes:

- **Daily routine:** movement, feeding, drinking, alert/flee, resting and jumping.
- **Actions + transitions:** select any motion and blend from the current pose.
- **Speed & steering:** continuous idle/walk/trot/run blending and gradual turns.
- **Uneven ground:** the same controller on a gently sloping, uneven test surface.
- **Native clips:** actual exported actions without the procedural wrapper.

Head/ear movement, manual head aim, ground correction, foot planting, camera
following and mesh LOD comparison can be switched independently. Native mode
deliberately omits those runtime layers. No game performance is inferred from
Blender's playback rate. The shortcut loads the external viewer script; opening
the blend directly does not automatically execute Python or install an add-on.

## Deliverables

Ignored local root: `artifacts/deer-behaviour-20261023`.

- `Deer - Game Ready Prototype.blend`: packed textures, rig and sixteen actions.
- `deer-motion-library.json` / `.npz`: rest skeleton, local pose samples, stance
  weights, gait timings and root tracks used by the reference viewer.
- `game-assets/Deer-Game-Prototype.glb`: four named mesh LODs sharing one material,
  two images, one skeleton and sixteen motions; approximately 7.34 MB.
- `game-assets/Deer-LOD0.glb` through `Deer-LOD3.glb`: standalone alternatives.

Use the combined file when shared textures/animation matter. Its named meshes
need an engine LOD group; it does not contain an automatically configured Unity
LODGroup or Animator. Display one LOD at a time. Standalone files duplicate their
embedded textures/animations and should not all be loaded as independent animals.

Geometry remains 13,999 / 6,000 / 2,200 / 700 triangles, one material, 2K colour
and 1K tangent normal. There are 34 bones, including the new non-deforming
`MotionRoot`. Every vertex has at most four normalized influences. Linear skinning
replaces the earlier preserve-volume Blender preview so the exported deformation
does not rely on Blender's dual-quaternion option.

The old model's roughly 59-degree sideways head bind is corrected through the
neck and baked into each mesh and the rig's rest pose. Measured neutral nose
heading is approximately -0.067 degrees relative to the body. UVs and accepted
texture pixels are retained. Hoof weights are tightened to their foot bones,
with a smooth ankle transition. This is an intentional rest/skin revision, not
the previous claim of an unchanged 33-bone rig.

## Motion sources and adaptations

| Action | Source and adaptation |
|---|---|
| Idle, Walk, Run | Quaternius deer Idle / Walk / Gallop; grounded and retargeted |
| Trot | Mesh2Motion horse Trot; retargeted to the deer proportions |
| Graze | Quaternius Eating, with neck reach and muzzle clearance |
| Drink | Mesh2Motion Eating adapted to a drinking posture |
| Alert | Quaternius Idle_2 plus optional procedural head aim |
| Rest | Mesh2Motion Sleep; folded-limb fit and ground contact adaptation |
| Get Up | Mesh2Motion lay_to_idle, sternal-rest entry and standing exit |
| Lie Down | Slower reversed adapted Get Up; prototype descent |
| Jump | Quaternius Gallop_Jump, including landing |
| Turn Left / Right | Mesh2Motion in-place stepping with explicit smooth 90-degree root turn |
| Hit Left / Right | Quaternius directional hit reactions |
| Death | Quaternius Death, with actual mesh floor clearance |

This uses authored motion, not newly generated neural motion or donor animal
geometry. Feeding/rest adaptations are not independent deer motion capture.
The head has limited procedural yaw/pitch and occasional ear flicks; there is no
new eyelid rig, chewing, swallowing or drinking-water simulation.

Sources are CC0:

- [Quaternius Ultimate Animated Animals](https://quaternius.com/packs/ultimateanimatedanimals.html).
  Local deer GLB SHA256 `fabf360b22eaccccd056ee99d698a9562d15aa245821f96e4fbad6db3751604e`.
- [Mesh2Motion art license](https://github.com/Mesh2Motion/mesh2motion-app/blob/653a9698f5f315523072f1c3ff3496100401317b/LICENSE-CC0.MD),
  including models, rigs and animation. Pinned app commit
  `653a9698f5f315523072f1c3ff3496100401317b`, `static/animations/horse-animations.glb`.
  Horse GLB SHA256 `c6f890c307e457b9aa7cceda1fbc1e39f8e6723ef340f23e3e667e122673b51c`.

The owned TripoSG shape and locally painted RealVis/SDXL/Marigold textures keep
the provenance and terms documented in [DEER_GAME_ASSET_PIPELINE.md](DEER_GAME_ASSET_PIPELINE.md).
CC0 motion does not make every input/model weight or the whole game CC0. No
rejected AnyTop animation or Hunyuan texture is admitted into this prototype.

## Controller and export boundary

`deer_behaviour_core.py` contains pose interpolation, forward kinematics,
fixed-length chain solving, stance foot correction and head/ear overlays. It
uses NumPy/mathutils as an offline reference. `deer-behaviour-viewer.py` supplies
the Blender UI, terrain height function, actor movement and frame callbacks.
It loads the motion arrays once, not compressed NPZ entries every frame.

Gaits use stride-phase-aligned blends and a smoothed speed. Pose changes use
short blends (longer for feeding), gradual actor steering and world-space stance
locks. Ground correction aligns planted hooves and solves their leg chains;
the slope demo also aligns the body to the local surface. The tested terrain is
gentle, not an arbitrary obstacle/course solver. Procedural look motion starts
from the corrected forward head. Death holds; lie-down proceeds to rest and
get-up proceeds to idle.

Portable actions are baked at 24 Hz with linear keys and explicit duplicate
loop endpoints. Most locomotion is in place: the engine/controller supplies
translation using gait speed. The dedicated turns have root yaw on `MotionRoot`.
The Blender wrapper's gaze, blending, foot locks and terrain queries are NOT
magically embedded into GLB animation. Those need equivalent runtime systems
when this animal is integrated into Unity. The routine demonstrates animation
states, not hunger, fear, population persistence, navigation or animal AI.

## Validation and limits

`check-deer-behaviour.py` checks all native keyed frame locations against the
motion library, finite normalized rotations, loop endpoints, root-turn signs,
sampled skinned surface clearance, all four meshes' budgets and influences,
and the actual GLB structures. Native surface floor samples are above the floor.
The real viewer handler runs 1,752 routine frames plus ten seconds on the test
slope, including native/manual mode switching and terminal death. Maximum
sampled stance-height error on the slope was approximately 4.78 cm; this is not
perfect foot contact or a no-sliding certification.

`check-deer-glb-roundtrip.py` reimports the actual LOD0 export and compares 160
surface vertices at each of seven poses against the final Blender asset. UV
seam order is handled by nearest-vertex comparison. Maximum difference was
approximately 0.0475 mm. This checks export deformation, not all engine imports.

Selected CPU pose renders catch major feeding/ground/rest defects. The GUI
launch reports the viewer ready; the owner is using another foreground app, so
no focus-stealing layout inspection is forced. Naturalness, all-pairs transitions,
body intersections, challenging terrain, production topology and game FPS still
need owner review. Lie-down/get-up and drinking are adapted placeholder motions.
No Unity player, game world, gameplay fixture or FPS benchmark was launched.

Repeat with portable Blender 4.5.15:

1. `build-deer-behaviour-library.py -- --asset <accepted blend> --deer <CC0 deer GLB> --root <root> --output-name <fresh blend>`.
2. `check-deer-behaviour.py -- --root <root> --blend <fresh blend> --export`.
3. `check-deer-glb-roundtrip.py -- <root> <fresh blend>`.
4. `open-deer-behaviour.py --root <root>` with ordinary Windows Python.

For steps 1-3 use Blender `--background --factory-startup --disable-autoexec
--python-exit-code 9 --python <script>`. Assets, evidence, logs and downloaded
motions remain ignored local artifacts; source backup does not back them up.
